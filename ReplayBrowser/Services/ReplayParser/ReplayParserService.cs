using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;
using ReplayBrowser.Models;
using ReplayBrowser.Models.Ingested;
using ReplayBrowser.Services.ReplayParser.Providers;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReplayBrowser.Services.ReplayParser;

public class ReplayParserService : IHostedService, IDisposable
{
    public static List<string> Queue = new();
    public static ConcurrentDictionary<string, double> DownloadProgress = new();
    public static ParserStatus Status = ParserStatus.Off;
    public static string Details = "";

    /// <summary>
    /// Since the Replay Meta file was added just yesterday, we want to cut off all replays that were uploaded before that.
    /// </summary>
    /// <returns></returns>
    public static DateTime CutOffDateTime = new(2024, 2, 17);

    public CancellationTokenSource TokenSource = new();

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _factory;


    /// <summary>
    /// In this case we wont just add it to the parsed replays, so it redownloads it every time.
    /// </summary>
    private const string YamlSerializerError = "Exception during deserialization";
    /// <summary>
    /// Holds the amount of retries for parsing a replay. If it fails 3 times, it will be added to the parsed replays.
    /// </summary>
    private Dictionary<string, int> _replayRetries = new();

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

        Status = ParserStatus.Idle;

#if !TESTING
        Task.Run(() => FetchReplays(TokenSource.Token, urLs), TokenSource.Token);
#endif
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await TokenSource.CancelAsync();
        Status = ParserStatus.Off;
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
            Status = ParserStatus.Discovering;
            Details = $"0/{storageUrls.Length}";
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

                Details = $"{Array.IndexOf(storageUrls, storageUrl) + 1}/{storageUrls.Length}";
            }

            var now = DateTime.Now;
            var nextRun = now.AddMinutes(10 - now.Minute % 10).AddSeconds(-now.Second);
            var delay = nextRun - now;
            await ConsumeQueue(token);
            Log.Information("Next run in " + delay.TotalMinutes + " minutes.");
            Status = ParserStatus.Idle;
            Details = "";
            await Task.Delay(delay, token);
        }
    }

    /// <summary>
    /// Requests the queue to be consumed. It will only consume the queue if it's not already being consumed.
    /// </summary>
    public bool RequestQueueConsumption()
    {
        if (Status != ParserStatus.Idle) return false;


        Task.Run(() => ConsumeQueue(TokenSource.Token), TokenSource.Token);
        return true;
    }

    private async Task ConsumeQueue(CancellationToken token)
    {
        if (Queue.Count > 0)
        {
            DownloadProgress.Clear();
        }

        var total = Queue.Count;
        var completed = 0;
        var parsedReplays = new List<Replay>();

        // Consume the queue.
        while (Queue.Count > 0)
        {
            var timeoutToken = new CancellationTokenSource(10000);
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken.Token);
            var startTime = DateTime.Now;
            // Clear the download progress.
            Details = $"{completed}/{total}";

            DownloadProgress.Clear();
            Status = ParserStatus.Downloading;
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
                        var progress = new Progress<double>(x =>
                        {
                            DownloadProgress[replay] = x;
                        });
                        client.DefaultRequestHeaders.Add("User-Agent", "ReplayBrowser");
                        Log.Information("Downloading " + replay);
                        var fileSize = await client.GetFileSizeAsync(replay);
                        // Check if the server supports range requests.
                        var supportsRange = (await client.SupportsRangeRequests(replay) && fileSize != -1);

                        Replay? parsedReplay = null;
                        try
                        {
                            if (supportsRange)
                            {
                                try
                                {
                                    // The server supports ranged processing!
                                    string[] files = ["_replay/replay_final.yml"];
                                    var extractedFiles = await ZipDownloader.ExtractFilesFromZipAsync(replay, files);
                                    completed++;
                                    Details = $"{completed}/{total}";
                                    parsedReplay = FinalizeReplayParse(new StreamReader(extractedFiles["_replay/replay_final.yml"]), replay);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "Error while partial downloading " + replay);
                                    // fuck it, we ball and try the normal method
                                    supportsRange = false;
                                }
                            }

                            if (!supportsRange)
                            {
                                var stream = await client.GetStreamAsync(replay, progress, token);
                                completed++;
                                Details = $"{completed}/{total}";
                                parsedReplay = ParseReplay(stream, replay);
                            }
                        }
                        catch (Exception e)
                        {
                            if (!_replayRetries.TryGetValue(replay, out var count))
                            {
                                _replayRetries.Add(replay, 1);
                                count = 1;
                            }
                            _replayRetries[replay]++;
                            Log.Error(e, "Error while parsing {Replay}. Retry count: {Count}", replay, count);
                            if (count >= 3)
                            {
                                await AddParsedReplayToDb(replay);
                                Log.Error("Failed to parse " + replay + " after 3 retries.");
                                return;
                            }
                            if (e.Message.Contains(YamlSerializerError)) return;

                            await AddParsedReplayToDb(replay);
                            return;
                        }
                        // See if the link matches the date regex, if it does set the date
                        var replayFileName = Path.GetFileName(replay);
                        var storageUrl = GetStorageUrlFromReplayLink(replay);
                        var match = storageUrl.ReplayRegexCompiled.Match(replayFileName);
                        if (match.Success)
                        {
                            try
                            {
                                var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
                                // Need to mark it as UTC, since the server is in UTC.
                                parsedReplay.Date = date.ToUniversalTime();
                            }
                            catch (FormatException)
                            {
                                var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                                parsedReplay.Date = date.ToUniversalTime();
                            }
                        }

                        // One more check to see if it's already in the database.
                        if (await IsReplayParsed(replay))
                        {
                            return;
                        }

                        await AddReplayToDb(parsedReplay);
                        await AddParsedReplayToDb(replay);
                        parsedReplays.Add(parsedReplay);
                        Log.Information("Parsed " + replay);
                        try
                        {
                            var webhookService = new WebhookService(_factory);
                            await webhookService.SendReplayToWebhooks(parsedReplay);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error while sending replay to webhooks.");
                        }
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

        // Set the status to idle.
        Status = ParserStatus.Idle;
        Details = "";
        Log.Information("Finished parsing replays.");
    }

    /// <summary>
    /// Parses a replay file and returns a Replay object.
    /// </summary>
    private Replay ParseReplay(Stream stream, string replayLink)
    {
        // Read the replay file and unzip it.
        var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
        var replayFile = zipArchive.GetEntry("_replay/replay_final.yml");

        if (replayFile == null)
        {
            throw new FileNotFoundException($"Replay is missing the replay_final.yml file.");
        }

        var replayStream = replayFile.Open();
        var reader = new StreamReader(replayStream);

        return FinalizeReplayParse(reader, replayLink);
    }

    private Replay FinalizeReplayParse(StreamReader stream, string replayLink)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yamlReplay = deserializer.Deserialize<YamlReplay>(stream);
        if (yamlReplay.Map == null && yamlReplay.Maps == null)
        {
            throw new Exception("Replay is not valid.");
        }

        var replay = Replay.FromYaml(yamlReplay, replayLink);

        var replayUrls = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>()!;
        if (replay.ServerId == Constants.UnsetServerId)
        {
            replay.ServerId = replayUrls.First(x => replay.Link!.Contains(x.Url)).FallBackServerId;
        }

        if (replay.ServerName == Constants.UnsetServerName)
        {
            replay.ServerName = replayUrls.First(x => replay.Link!.Contains(x.Url)).FallBackServerName;
        }

        if (yamlReplay.RoundEndPlayers == null)
            return replay;

        var jobs = GetDbContext().JobDepartments.ToList();

        replay.RoundParticipants!.ForEach(
            p => p.Players!
                .Where(pl => pl.JobPrototypes.Count != 0)
                .ToList()
                .ForEach(
                    pl => pl.EffectiveJobId = jobs.SingleOrDefault(j => j.Job == pl.JobPrototypes[0])?.Id
                )
        );

        // Check for GDPRed accounts
        var gdprGuids = GetDbContext().GdprRequests.Select(x => x.Guid).ToList();

        foreach (var redact in gdprGuids)
        {
            replay.RedactInformation(redact, true);
        }

        var redacted = replay.RoundParticipants!.Where(p => p.PlayerGuid == Guid.Empty);
        if (redacted.Any())
            replay.RedactCleanup();

        return replay;
    }

    public StorageUrl GetStorageUrlFromReplayLink(string replayLink)
    {
        var replayUrls = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>()!;
        var fetched = replayUrls.First(x => replayLink.Contains(x.Url));
        fetched.CompileRegex();
        return fetched;
    }

    public async Task AddReplayToQueue(string replay)
    {
        // Use regex to check and retrieve the date from the file name.
        var storageUrl = GetStorageUrlFromReplayLink(replay);
        var fileName = Path.GetFileName(replay);
        var match = storageUrl.ReplayRegexCompiled.Match(fileName);
        if (match.Success)
        {
            try
            {
                var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
                if (date < CutOffDateTime)
                {
                    return;
                }
            }
            catch (FormatException)
            {
                var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (date < CutOffDateTime)
                {
                    return;
                }
            }

        } else
        {
            Log.Warning("Replay " + replay + " does not match the regex.");
            return;
        }

        // If it's already in the database, skip it.
        if (await IsReplayParsed(replay))
        {
            return;
        }
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
