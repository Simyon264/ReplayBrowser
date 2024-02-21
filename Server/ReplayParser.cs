using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
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
        while (!token.IsCancellationRequested)
        {
            // Consume the queue.
            while (Queue.Count > 0)
            {
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
                            var client = new HttpClient();
                            Console.WriteLine("Downloading " + replay);
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
                            Console.WriteLine("Parsed " + replay);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }, token));
                    
                    // Wait for all tasks to finish.
                    await Task.WhenAll(tasks);
                }
            }
            
            await Task.Delay(5000, token);
        }   
    }
    
    /// <summary>
    /// Handles fetching replays from the remote storage.
    /// </summary>
    public static async Task FetchReplays(CancellationToken token, string storageUrl)
    {
        while (!token.IsCancellationRequested)
        {
            Console.WriteLine("Fetching replays...");
            try
            {
                await RetrieveFilesRecursive(storageUrl, token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            var now = DateTime.Now;
            var nextRun = now.AddMinutes(10 - now.Minute % 10).AddSeconds(-now.Second);
            var delay = nextRun - now;
            await Task.Delay(delay, token);
        }
    }

    private static async Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token)
    {
        try
        {
            Console.WriteLine("Retrieving files from " + directoryUrl);
            var client = new HttpClient();
            var htmlContent = await client.GetStringAsync(directoryUrl, token);
            var document = new HtmlDocument();
            document.LoadHtml(htmlContent);
            
            var links = document.DocumentNode.SelectNodes("//a[@href]");
            if (links == null)
            {
                Console.WriteLine("No links found on " + directoryUrl);
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
                        Console.WriteLine("Adding " + href + " to the queue.");
                        Queue.Add(href);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
    public static List<Replay> SearchReplays(SearchMode mode, string query, List<Replay> replays)
    {
        switch (mode)
        {
            case SearchMode.Map:
                return replays.Where(x => x.Map.Contains(query)).ToList();
            case SearchMode.Gamemode:
                return replays.Where(x => x.Gamemode.Contains(query)).ToList();
            case SearchMode.ServerId:
                return replays.Where(x => x.ServerId.Contains(query)).ToList();
            case SearchMode.Guid:
                return replays.Where(x => (x.RoundEndPlayers ?? []).Any(y => y.PlayerGuid.ToString().Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
            case SearchMode.PlayerIcName:
                return replays.Where(x => (x.RoundEndPlayers ?? []).Any(y => y.PlayerIcName.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
            case SearchMode.PlayerOocName:
                return replays.Where(x => (x.RoundEndPlayers ?? []).Any(y => y.PlayerOocName.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
            case SearchMode.RoundEndText:
                return replays.Where(x => x.RoundEndText != null && x.RoundEndText.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            default:
                throw new NotImplementedException();
        }
    }
}