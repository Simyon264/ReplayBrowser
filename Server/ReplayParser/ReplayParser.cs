using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Server.Api;
using Server.Metrics;
using Server.ReplayLoading;
using Shared;
using Shared.Models;
using YamlDotNet.Serialization;

namespace Server.ReplayParser;

public static class ReplayParser
{
    public static ReplayDbContext Context { get; set; }
    public static ReplayMetrics Metrics { get; set; }

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
                        Metrics.ReplayParsed(replay);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error while parsing " + replay);
                        Metrics.ReplayError(replay);
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
    public static async Task FetchReplays(CancellationToken token, StorageUrl[] storageUrls)
    {
        while (!token.IsCancellationRequested)
        {
            foreach (var storageUrl in storageUrls)
            {
                Log.Information("Fetching replays from " + storageUrl);
                try
                {
                    var provider = ReplayProviderFactory.GetProvider(storageUrl.Provider);
                    await provider.RetrieveFilesRecursive(storageUrl.Url, token);
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
    
    public static async Task AddReplayToQueue(string replay)
    {
        // Use regex to check and retrieve the date from the file name.
        var fileName = Path.GetFileName(replay);
        var match = RegexList.ReplayRegex.Match(fileName);
        if (match.Success)
        {
            var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
            if (date < CutOffDateTime)
            {
                return;
            }
        }
        
        // If it's already in the database, skip it.
        if (await IsReplayParsed(replay))
        {
            return;
        }
        Log.Information("Adding " + replay + " to the queue.");
        // Check if it's already in the queue.
        if (!Queue.Contains(replay))
        {
            Queue.Add(replay);
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
}

public class StorageUrl
{
    public string Url { get; set; }
    public string Provider { get; set; }

    public override string ToString()
    {
        return Url;
    }
}