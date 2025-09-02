using System.Diagnostics;
using System.Globalization;
using System.Text;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;
using ReplayBrowser.Services;
using ReplayBrowser.Services.ReplayParser;
using Serilog;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("api/Replay/")]
[Authorize]
public class ReplayController : Controller
{
    private readonly ReplayDbContext _dbContext;
    private readonly AccountService _accountService;
    private readonly ReplayHelper _replayHelper;
    private readonly ReplayParserService _replayParserService;
    private readonly IServiceScopeFactory _factory;
    private readonly Ss14ApiHelper _ss14ApiHelper;
    private readonly IMemoryCache _memoryCache;

    public ReplayController(IMemoryCache cache, Ss14ApiHelper ss14ApiHelper, ReplayDbContext dbContext, AccountService accountService, ReplayHelper replayHelper, ReplayParserService replayParserService, IServiceScopeFactory factory)
    {
        _dbContext = dbContext;
        _accountService = accountService;
        _replayHelper = replayHelper;
        _replayParserService = replayParserService;
        _factory = factory;
        _ss14ApiHelper = ss14ApiHelper;
        _memoryCache = cache;
    }

    [HttpGet("{replayId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReplay(int replayId)
    {
        var authState = new AuthenticationState(HttpContext.User);
        var replay = await _replayHelper.GetReplay(replayId, authState);
        if (replay == null)
        {
            return NotFound();
        }

        return Ok(replay);
    }

    /// <summary>
    /// Tells the server to parse a replay based on a provided url.
    /// </summary>
    /// <returns></returns>
    [HttpPost("replay/parse")]
    public async Task<IActionResult> ParseReplay(
        [FromQuery] string url
    )
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guidRequestor = AccountHelper.GetAccountGuid(User);

        var requestor = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Guid == guidRequestor);

        if (requestor == null)
        {
            return NotFound("Account is null. This should not happen.");
        }

        if (!requestor.IsAdmin)
            return Unauthorized("You are not an admin.");

        ReplayParserService.Queue.Add(url);
        if (_replayParserService.RequestQueueConsumption())
            return Ok();

        return BadRequest("The replay parser is currently busy.");
    }

    /// <summary>
    /// Deletes all stored profiles for a server group.
    /// </summary>

    [HttpDelete("profile/delete/{serverId}")]
    public async Task<IActionResult> DeleteProfile(string serverId)
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guidRequestor = AccountHelper.GetAccountGuid(User);

        var requestor = await _dbContext.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guidRequestor);

        if (requestor == null)
        {
            return NotFound("Account is null. This should not happen.");
        }

        if (!requestor.IsAdmin)
            return Unauthorized("You are not an admin.");

        var players = await _dbContext.Replays
            .Where(r => r.ServerId == serverId)
            .Include(r => r.RoundParticipants)
            .Where(r => r.RoundParticipants != null)
            .SelectMany(r => r.RoundParticipants!)
            .Select(p => p.PlayerGuid)
            .Distinct()
            .ToListAsync();

        foreach (var player in players)
        {
            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "CharacterData"
                WHERE "CollectedPlayerDataPlayerGuid" = {player};
                """
            );

            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "JobCountData"
                WHERE "CollectedPlayerDataPlayerGuid" = {player};
                """
            );
        }

        await _dbContext.PlayerProfiles
            .Where(p => players.Contains(p.PlayerGuid))
            .ExecuteDeleteAsync();

        return Ok();
    }

    [HttpGet("profile/{profileGuid:guid}")]
    public async Task<IActionResult> GetPlayerData(Guid profileGuid)
    {
        // ok very jank, we construct a AuthenticationState object from the current user
        var authState = new AuthenticationState(HttpContext.User);

        try
        {
            return Ok(await _replayHelper.GetPlayerProfile(profileGuid, authState));
        }
        catch (UnauthorizedAccessException e)
        {
            return Unauthorized(e.Message);
        }
    }

    private struct Line
    {
        public float FontSize { get; set; }
        public string Text { get; set; }
        public float MarginTop { get; set; }

        public Line(string text)
        {
            Text = text;
            FontSize = 36;
            MarginTop = 0;
        }
    }

    [HttpGet("profile/{profileGuid:guid}/video")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVideoStat(Guid profileGuid)
    {
        CollectedPlayerData? data = null;

        var authState = new AuthenticationState(HttpContext.User);

        try
        {
            data = await _replayHelper.GetPlayerProfile(profileGuid, authState);
        }
        catch (UnauthorizedAccessException e)
        {
            return Unauthorized(e.Message);
        }

        var cacheKey = $"profile-vid-{profileGuid}";
        if (_memoryCache.TryGetValue(cacheKey, out var result))
        {
            if (System.IO.File.Exists((string)result!))
            {
                return PhysicalFile((string)result, "video/mp4");
            }
            else
            {
                _memoryCache.Remove(cacheKey);
            }
        }

        var playerdata = await _ss14ApiHelper.FetchPlayerDataFromGuid(data.PlayerGuid);
        var username = playerdata.Username;

        var mostPlayedCharacters = data.Characters
            .Where(x => !x.CharacterName.Contains('('))
            .OrderByDescending(x => x.RoundsPlayed);

        var mostPlayedJobs = data.JobCount
            .OrderByDescending(x => x.RoundsPlayed);

        var lines = new List<Line>()
        {
            new Line($"Stats For {username}")
            {
                FontSize = 90
            },
            new Line($"Last Seen: {data.LastSeen.Humanize()} ({data.LastSeen})")
            {
                MarginTop = 30
            },
            new Line($"Rounds Played: {data.TotalRoundsPlayed}"),
            new Line($"Antag Rounds Played: {data.TotalAntagRoundsPlayed}"),
        };

        lines.Add(new Line("Top 5 Characters:")
        {
            MarginTop = 30,
            FontSize = 60
        });

        var isFirst = true;
        foreach (var character in mostPlayedCharacters.Take(5))
        {
            lines.Add(new Line($" > {character.CharacterName} ({character.RoundsPlayed})")
            {
                MarginTop = isFirst ? 10 : 0,
            });
            isFirst = false;
        }

        isFirst = true;

        lines.Add(new Line("Top 5 Jobs:")
        {
            MarginTop = 30,
            FontSize = 60
        });

        foreach (var job in mostPlayedJobs.Take(5))
        {
            lines.Add(new Line($" > {job.JobPrototype} ({job.RoundsPlayed})")
            {
                MarginTop = isFirst ? 10 : 0,
            });
            isFirst = false;
        }

        var baseVideoPath = Path.GetFullPath("./profile_base_video.mp4");
        var fontPath = Path.GetFullPath("./profile_base_font.ttf").Replace("\\", "/");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");

        const float initialDelay = 13.1f;
        var drawTextFilters = new List<string>();
        var marginsSoFar = 0f;
        for (var i = 0; i < lines.Count; i++)
        {
            marginsSoFar += lines[i].MarginTop;

            var delay = initialDelay + (i * 0.5f);
            var filter = $"drawtext=fontfile='{EscapeText(fontPath)}':text='{EscapeText(lines[i].Text)}':x=(w-text_w)/2:y={((i+1)*50) + marginsSoFar}:fontsize={lines[i].FontSize}:fontcolor=white:enable='gte(t\\,{delay.ToString().Replace(',', '.')})'";
            drawTextFilters.Add(filter);
        }

        var ffmpegArgs = $"-y -i \"{baseVideoPath}\" -vf \"{string.Join(",", drawTextFilters)}\" -b:v 4M -preset fast -c:a copy \"{outputPath}\"";

        var processInfo = new ProcessStartInfo("ffmpeg", ffmpegArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processInfo;
        var stderr = new StringBuilder();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stderr.AppendLine(e.Data);
            }
        };


        Log.Information("Starting ffmpeg with args: {args}", ffmpegArgs);

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var errorOutput = stderr.ToString();
            Log.Error("FFmpeg failed: {error}", errorOutput);
            return StatusCode(500, $"FFmpeg failed with exit code {process.ExitCode}");
        }

        _memoryCache.Set(cacheKey, outputPath, DateTimeOffset.MaxValue);
        return PhysicalFile(outputPath, "video/mp4");
    }

    private static string EscapeText(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace("'", "\\'")
            .Replace(",", "\\,")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }


    /// <summary>
    /// Marks a profile "watched" for the current user.
    /// </summary>
    /// <returns>True if the profile is now watched, false if it is now unwatched.</returns>
    [HttpPost("watch/{profileGuid:guid}")]
    public async Task<IActionResult> WatchProfile(Guid profileGuid)
    {
        var guid = AccountHelper.GetAccountGuid(HttpContext.User);
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Guid == guid);

        if (account == null)
        {
            return Unauthorized();
        }

        var isWatched = account.SavedProfiles.Contains(profileGuid);

        if (!account.SavedProfiles.Remove(profileGuid))
        {
            account.SavedProfiles.Add(profileGuid);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(!isWatched);
    }


    /// <summary>
    /// Marks a replay as a favorite for the current user.
    /// </summary>
    /// <returns>True if the replay is now favorited, false if it is now unfavorited.</returns>
    [HttpPost("favourite/{replayId}")]
    public async Task<IActionResult> FavoriteReplay(int replayId)
    {
        var guid = AccountHelper.GetAccountGuid(HttpContext.User);
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Guid == guid);

        if (account == null)
        {
            return Unauthorized();
        }

        var replay = await _dbContext.Replays.FindAsync(replayId);
        if (replay == null)
        {
            return NotFound();
        }

        var isFavorited = account.FavoriteReplays.Contains(replayId);

        if (!account.FavoriteReplays.Remove(replayId))
        {
            account.FavoriteReplays.Add(replayId);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(!isFavorited);
    }

    [HttpPost("replay/upload")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadReplay(
        IFormCollection form
    )
    {
        if (!CheckAuthenticationTokenServerApi())
        {
            return Unauthorized("No valid token provided.");
        }

        var file = form.Files.FirstOrDefault();
        if (file == null)
        {
            return BadRequest("No file provided.");
        }

        var reader = new StreamReader(file.OpenReadStream());
        Replay? replay = null;
        try
        {
            var replayYaml = _replayParserService.ParseReplay(reader);
            replay = _replayParserService.ParseReplayYaml(replayYaml, null);
            var replayFileName = Path.GetFileName(replay.Link);
            var storageUrl = _replayParserService.GetStorageUrlFromReplayLink(replay.Link);
            var match = storageUrl.ReplayRegexCompiled.Match(replayFileName);
            if (match.Success)
            {
                try
                {
                    var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy_MM_dd-HH_mm", CultureInfo.InvariantCulture);
                    replay.Date = date.ToUniversalTime();
                }
                catch (FormatException)
                {
                    var date = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    replay.Date = date.ToUniversalTime();
                }
            }
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
        await _dbContext.Replays.AddAsync(replay);
        await _dbContext.SaveChangesAsync();
        Log.Information("Parsed " + replay);
        try
        {
            var webhookService = new WebhookService(_factory);
            await webhookService.SendReplayToWebhooks(replay);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while sending replay to webhooks.");
        }

        return Ok();
    }

    private bool CheckAuthenticationTokenServerApi()
    {
        var token = Request.Headers.Authorization;
        if (token.Count == 0)
        {
            return false;
        }

        var tokenString = token.ToString().Split(" ")[1];

        return _dbContext.ServerTokens.Any(t => t.Token == tokenString);
    }
}