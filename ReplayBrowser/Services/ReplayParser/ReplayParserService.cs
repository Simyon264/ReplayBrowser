using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Services.ReplayParser.Providers;
using Serilog;
using YamlDotNet.Serialization;

namespace ReplayBrowser.Services.ReplayParser;

public class ReplayParserService : IHostedService, IDisposable
{
    public static List<string> Queue = new();
    
    /// <summary>
    /// Since the Replay Meta file was added just yesterday, we want to cut off all replays that were uploaded before that.
    /// </summary>
    /// <returns></returns>
    public static DateTime CutOffDateTime = new(2024, 2, 17);

    public CancellationTokenSource TokenSource = new();
    
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _factory;
    
    
    public ReplayParserService(IConfiguration configuration, IServiceScopeFactory factory)
    {
        _configuration = configuration;
        _factory = factory;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var urLs = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>();
        if (urLs == null)
        {
            throw new Exception("No replay URLs found in appsettings.json. Please set ReplayUrls to an array of URLs.");
        }
        
        Task.Run(() => FetchReplays(TokenSource.Token, urLs), TokenSource.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await TokenSource.CancelAsync();
    }

    public void Dispose()
    {
        TokenSource.Dispose();
    }
    
    /// <summary>
    /// Handles fetching replays from the remote storage.
    /// </summary>
    private async Task FetchReplays(CancellationToken token, StorageUrl[] storageUrls)
    {
        while (!token.IsCancellationRequested)
        {
            foreach (var storageUrl in storageUrls)
            {
                Log.Information("Fetching replays from " + storageUrl);
                try
                {
                    var provider = ReplayProviderFactory.GetProvider(storageUrl.Provider, this);
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
    
    private async Task ConsumeQueue(CancellationToken token)
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
                            var client = new HttpClient();
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
    /// Parses a replay file and returns a Replay object.
    /// </summary>
    /// <param name="fileStream">The file.</param>
    /// <returns>A parsed replay.</returns>
    /// <exception cref="FileNotFoundException">The input file path is not valid.</exception>
    private Replay ParseReplay(Stream fileStream)
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

    public async Task AddReplayToQueue(string replay)
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

    private async Task<bool> IsReplayParsed(string replay)
    {
        await using var db = GetDbContext();
        return await db.ParsedReplays.AnyAsync(x => x.Name == replay);
    }
    
    private async Task AddParsedReplayToDb(string replay)
    {
        await using var db = GetDbContext();
        await db.ParsedReplays.AddAsync(new ParsedReplay
        {
            Name = replay
        });
        await db.SaveChangesAsync();
    }
    
    private async Task AddReplayToDb(Replay replay)
    {
        await using var db = GetDbContext();
        await db.Replays.AddAsync(replay);
        await db.SaveChangesAsync();
    }
    
    private ReplayDbContext GetDbContext()
    {
        var scope = _factory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
    }
}