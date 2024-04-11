using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Serilog;
using Server.Api;
using Shared;
using Shared.Models;
using YamlDotNet.Serialization;

namespace Server;

public static class ReplayParser
{
    public static ReplayDbContext Context { get; set; }
    
    public static List<string> Queue = new();
    /// <summary>
    /// Since the Replay Meta file was added just yesterday, we want to cut off all replays that were uploaded before that.
    /// </summary>
    /// <returns></returns>
    public static DateTime CutOffDateTime = new(2024, 2, 17);
    
    public static Task<bool> IsReplayParsed(string replay)
    {
        lock (Context)
        {
            return Context.ParsedReplays.AnyAsync(x => x.Name == replay);
        }
    }
    
    public static Task AddReplayToDb(Replay replay)
    {
        lock (Context)
        {
            Context.Replays.Add(replay);
            return Context.SaveChangesAsync();
        }
    }
    
    public static Task AddParsedReplayToDb(string replay)
    {
        lock (Context)
        {
            Context.ParsedReplays.Add(new ParsedReplay {Name = replay});
            return Context.SaveChangesAsync();
        }
    }
    
    public static async Task ConsumeQueue(CancellationToken token)
    {
        // Consume the queue.
        while (Queue.Count > 0)
        {
            var timeoutToken = new CancellationTokenSource(10000);
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken.Token);
            var startTime = DateTime.Now;
                
                // Since replays are like 200mb long, we want to parrallelize this.
                var tasks = new List<Task>();
                for (var i = 0; i < 10; i++)
                {
                    if (Queue.Count == 0)
                    {
                        break;
                    }
                    var replay = Queue[0];
                    Queue.RemoveAt(0);
                    // If it's already in the database, skip it.
                    if (await IsReplayParsed(replay))
                    {
                        continue;
                    }
                    
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var client = CreateHttpClient();
                            Log.Information("Downloading " + replay);
                            var fileStream = await client.GetStreamAsync(replay, token);
                            Replay? parsedReplay = null;
                            try
                            {
                                parsedReplay = ParseReplay(fileStream);
                            }
                            catch (Exception e)
                            {
                                // Ignore
                                await AddParsedReplayToDb(replay);
                                return;
                            }
                            parsedReplay.Link = replay;
                            // See if the link matches the date regex, if it does set the date
                            var replayFileName = Path.GetFileName(replay);
                            var match = RegexList.ReplayRegex.Match(replayFileName);
                            if (match.Success)
                            {
                                var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
                                // Need to mark it as UTC, since the server is in UTC.
                                parsedReplay.Date = date.ToUniversalTime();
                            }
                            
                        // One more check to see if it's already in the database.
                        if (await IsReplayParsed(replay))
                        {
                            return;
                        }
                            
                        await AddReplayToDb(parsedReplay);
                        await AddParsedReplayToDb(replay);
                        Log.Information("Parsed " + replay);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error while parsing " + replay);
                    }
                }, tokenSource.Token));
            }
                
            // If the download takes too long, cancel it.
            // 10 minutes should be enough
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(600000, token));
            await timeoutToken.CancelAsync(); 
            // Cancel the timeout token, so the background tasks cancel as well.
                
            // If we timed out, log a warning.
            if (DateTime.Now - startTime > TimeSpan.FromMinutes(10))
            {
                Log.Warning("Parsing took too long for " + string.Join(", ", tasks.Select(x => x.Id)));
            }
        }
    }
    
    /// <summary>
    /// Handles fetching replays from the remote storage.
    /// </summary>
    public static async Task FetchReplays(CancellationToken token, string[] storageUrls)
    {
        while (!token.IsCancellationRequested)
        {
            foreach (var storageUrl in storageUrls)
            {
                Log.Information("Fetching replays from " + storageUrl);
                try
                {
                    await RetrieveFilesRecursive(storageUrl, token);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error while fetching replays from " + storageUrl);
                }
            }
            
            var now = DateTime.Now;
            var nextRun = now.AddMinutes(10 - now.Minute % 10).AddSeconds(-now.Second);
            var delay = nextRun - now;
            ConsumeQueue(token);
            Log.Information("Next run in " + delay.TotalMinutes + " minutes.");
            await Task.Delay(delay, token);
        }
    }

    private static async Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token)
    {
        try
        {
            Log.Information("Retrieving files from " + directoryUrl);
            var client = CreateHttpClient();
            var htmlContent = await client.GetStringAsync(directoryUrl, token);
            var document = new HtmlDocument();
            document.LoadHtml(htmlContent);
            
            var links = document.DocumentNode.SelectNodes("//a[@href]");
            if (links == null)
            {
                Log.Information("No links found on " + directoryUrl + ".");
                return;
            }
            
            foreach (var link in links)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                
                var href = link.Attributes["href"].Value;

                if (href.StartsWith("..", StringComparison.Ordinal))
                {
                    continue;
                }
                
                if (!Uri.TryCreate(href, UriKind.Absolute, out _))
                {
                    href = new Uri(new Uri(directoryUrl), href).ToString();
                }

                if (href.EndsWith("/", StringComparison.Ordinal))
                {
                    await RetrieveFilesRecursive(href, token);
                }
                
                if (href.EndsWith(".zip", StringComparison.Ordinal))
                {
                    // Use regex to check and retrieve the date from the file name.
                    var fileName = Path.GetFileName(href);
                    var match = RegexList.ReplayRegex.Match(fileName);
                    if (match.Success)
                    {
                        var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
                        if (date < CutOffDateTime)
                        {
                            continue;
                        }
                        
                        // If it's already in the database, skip it.
                        if (await IsReplayParsed(href))
                        {
                            continue;
                        }
                        Log.Information("Adding " + href + " to the queue.");
                        // Check if it's already in the queue.
                        if (!Queue.Contains(href))
                        {
                            Queue.Add(href);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while retrieving files from " + directoryUrl);
            // We don't care about the exception, we just want to return the files we have.
        }
    }
    
    /// <summary>
    /// Parses a replay file and returns a Replay object.
    /// </summary>
    /// <param name="fileStream">The file.</param>
    /// <returns>A parsed replay.</returns>
    /// <exception cref="FileNotFoundException">The input file path is not valid.</exception>
    public static Replay ParseReplay(Stream fileStream)
    {
        // Read the replay file and unzip it.
        var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);
        var replayFile = zipArchive.GetEntry("_replay/replay_final.yml");
        
        if (replayFile == null)
        {
            throw new FileNotFoundException($"Replay is missing the replay_final.yml file.");
        }
        
        var replayStream = replayFile.Open();
        var reader = new StreamReader(replayStream);

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        var replay = deserializer.Deserialize<Replay>(reader);
        if (replay.Map == null)
        {
            throw new Exception("Replay is not valid.");
        }
        
        return replay;
    }

    /// <summary>
    /// Creates a new HttpClient with the necessary headers, user agent etc.
    /// </summary>
    /// <returns></returns>
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "ReplayBrowser");
        return client;
    }
    
    /// <summary>
    /// Searches a list of replays for a specific query.
    /// </summary>
    /// <param name="mode">The search mode.</param>
    /// <param name="query">The search query.</param>
    /// <param name="replays">The list of replays to search.</param>
    /// <returns>
    /// A list of replays that match the search query.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// Thrown when the search mode is not implemented.
    /// </exception>
    public static (List<Replay>, int) SearchReplays(SearchMode mode, string query, ReplayDbContext context, int page, int pageSize)
    {
        var queryable = context.Replays.AsQueryable();
    
        IIncludableQueryable<Player, Replay?>? players;
        IQueryable<int?>? replayIds;
        switch (mode)
        {
            case SearchMode.Map:
                queryable = queryable.Where(x => x.Map.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.Gamemode:
                queryable = queryable.Where(x => x.Gamemode.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.ServerId:
                queryable = queryable.Where(x => x.ServerId.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.Guid:
                players = context.Players
                    .Where(p => p.PlayerGuid.ToString().ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.PlayerIcName:
                players = context.Players
                    .Where(p => p.PlayerIcName.ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.PlayerOocName:
                players = Context.Players
                    .Where(p => p.PlayerOocName.ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = Context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.RoundEndText:
                // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall (its lying, this works)
                queryable = queryable.Where(x => x.RoundEndTextSearchVector.Matches(query));
                break;
            case SearchMode.ServerName:
                queryable = queryable.Where(x => x.ServerName != null && x.ServerName.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.RoundId:
                queryable = queryable.Where(x => x.RoundId != null && x.RoundId.ToString().Contains(query));
                break;
            default:
                throw new NotImplementedException();
        }
    
        var totalItems = queryable.Count();
        
        // Apply pagination on the database query
        return (queryable
                .Include(r => r.RoundEndPlayers)
                .OrderByDescending(r => r.Date ?? DateTime.MinValue).Take(Constants.SearchLimit).ToList()
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList(),
            totalItems);
    }
}
