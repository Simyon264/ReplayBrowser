using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Server.Helpers;
using Shared;
using Shared.Models;
using Shared.Models.Account;
using Action = System.Action;

namespace Server.Api;

/// <summary>
/// Contains endpoints for data retrieval. Such as search completions, leaderboards, and more.
/// </summary>
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly ReplayDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Ss14ApiHelper _apiHelper;
    public DataController(ReplayDbContext context, IMemoryCache cache, Ss14ApiHelper apiHelper)
    {
        _context = context;
        _cache = cache;
        _apiHelper = apiHelper;
    }

    [HttpGet]
    [Route("player-data")]
    [Authorize(Policy = "TokenBased")]
    public async Task<ActionResult> GetPlayerData(
        [FromQuery] string guid,
        [FromHeader] Guid? accountGuid
    )
    {
        var playerGuid = Guid.Parse(guid);
        if (playerGuid == Guid.Empty)
        {
            return BadRequest("Invalid GUID");
        }
        
        var accountCaller = await _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Guid == accountGuid);
        
        var accountRequested = await _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Guid == playerGuid);
        
        if (accountRequested is { Settings.RedactInformation: true })
        {
            if (accountCaller == null || accountCaller.Guid != playerGuid)
            {
                switch (accountCaller)
                {
                    case null:
                        return Unauthorized();
                    case { IsAdmin: false }:
                        return Unauthorized();
                }
            }
        }

        var replays = (await _context.Players
            .Where(p => p.PlayerGuid == playerGuid)
            .Include(p => p.Replay)
            .Include(r => r.Replay.RoundEndPlayers)
            .Select(p => p.Replay)
            .ToListAsync()
            ).DistinctBy(p => p.Id);

        var charactersPlayed = new List<CharacterData>();
        var totalPlaytime = TimeSpan.Zero;
        var totalRoundsPlayed = new List<int>();
        var totalAntagRoundsPlayed = new List<int>();
        var lastSeen = DateTime.MinValue;
        var jobCount = new List<JobCountData>();
        
        foreach (var replay in replays)
        {
            if (replay == null)
            {
                Log.Warning("Replay is null for player with GUID {PlayerGuid}", playerGuid);
                continue;
            }
            
            if (replay.RoundEndPlayers == null)
                continue;
            
            if (replay.Date > lastSeen) // Update last seen
            {
                lastSeen = (DateTime)replay.Date;
            }
            
            var characters = replay.RoundEndPlayers
                .Where(p => p.PlayerGuid == playerGuid)
                .Select(p => p.PlayerIcName)
                .Distinct()
                .ToList();

            foreach (var character in characters)
            {
                // Check if the character is already in the list
                var characterData = charactersPlayed.FirstOrDefault(c => c.CharacterName == character);
                if (characterData == null)
                {
                    charactersPlayed.Add(new CharacterData()
                    {
                        CharacterName = character,
                        LastPlayed = (DateTime)replay.Date,
                        RoundsPlayed = 1
                    });
                }
                else
                {
                    characterData.RoundsPlayed++;
                    if (replay.Date > characterData.LastPlayed)
                    {
                        characterData.LastPlayed = (DateTime)replay.Date;
                    }
                }
            }
            
            var jobPrototypes = replay.RoundEndPlayers
                .Where(p => p.PlayerGuid == playerGuid)
                .Select(p => p.JobPrototypes)
                .Distinct()
                .ToList();

            foreach (var jobPrototypeList in jobPrototypes)
            {
                foreach (var jobPrototype in jobPrototypeList)
                {
                    var jobData = jobCount.FirstOrDefault(j => j.JobPrototype == jobPrototype);
                    if (jobData == null)
                    {
                        jobCount.Add(new JobCountData()
                        {
                            JobPrototype = jobPrototype,
                            RoundsPlayed = 1,
                            LastPlayed = (DateTime)replay.Date
                        });
                    }
                    else
                    {
                        jobData.RoundsPlayed++;
                        if (replay.Date > jobData.LastPlayed)
                        {
                            jobData.LastPlayed = (DateTime)replay.Date;
                        }
                    }
                }
            }
            
            // Since duration is a string (example 02:04:51.4258419), we need to parse it.
            if (TimeSpan.TryParse(replay.Duration, out var duration))
            {
                totalPlaytime += duration;
            }
            else
            {
                Log.Warning("Unable to parse duration {Duration} for replay with ID {ReplayId}", replay.Duration, replay.Id);
            }

            if (!totalRoundsPlayed.Contains(replay.Id))
            {
                totalRoundsPlayed.Add(replay.Id);
            }
            
            if (replay.RoundEndPlayers.Any(p => p.PlayerGuid == playerGuid && p.Antag))
            {
                if (!totalAntagRoundsPlayed.Contains(replay.Id))
                {
                    totalAntagRoundsPlayed.Add(replay.Id);
                }
            }
        }
        
        CollectedPlayerData collectedPlayerData = new()
        {
            PlayerData = new PlayerData()
            {
                PlayerGuid = playerGuid,
                Username = (await _apiHelper.FetchPlayerDataFromGuid(playerGuid))?.Username ??
                           "Unable to fetch username (API error)"
            },
            Characters = charactersPlayed,
            TotalEstimatedPlaytime = totalPlaytime,
            TotalRoundsPlayed = totalRoundsPlayed.Count,
            TotalAntagRoundsPlayed = totalAntagRoundsPlayed.Count,
            LastSeen = lastSeen,
            JobCount = jobCount
        };
        
        // Add history entry
        if (accountCaller != null)
        {
            accountCaller.History.Add(new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Shared.Models.Account.Action), Shared.Models.Account.Action.ProfileViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Player GUID: {playerGuid}"
            });
        } else
        {
            var systemAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Guid == Guid.Empty);
            systemAccount!.History.Add(new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Shared.Models.Account.Action), Shared.Models.Account.Action.ProfileViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Player GUID: {playerGuid}"
            });
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(collectedPlayerData);
    }
    
    /// <summary>
    /// Provides a list of usernames which start with the given username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("username-completion")]
    public async Task<ActionResult> GetUsernameCompletion(
        [FromQuery] string username
    )
    {
        var completions = await _context.Players
            .Where(p => p.PlayerOocName.ToLower().StartsWith(username.ToLower()))
            .Select(p => p.PlayerOocName)
            .Distinct() // Remove duplicates
            .Take(10)
            .ToListAsync();

        return Ok(completions);
    }

    /// <summary>
    /// Tries to find a player with the given username. If found, returns the player's GUID.
    /// </summary>
    [HttpGet]
    [Route("has-profile")]
    [Authorize(Policy = "TokenBased")]
    public async Task<PlayerData> HasProfile(
        [FromQuery] string username,
        [FromHeader] Guid? accountGuid
    )
    {
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.PlayerOocName.ToLower() == username.ToLower());
        if (player == null)
            return new PlayerData()
            {
                PlayerGuid = Guid.Empty,
                Username = "NOT FOUND"
            };
        
        var account = await _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Username == username);
        
        if (account != null && account.Settings.RedactInformation && account.Guid != accountGuid)
        {
            var requestor = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Guid == accountGuid);

            if (requestor == null || !requestor.IsAdmin)
            {
                return new PlayerData()
                {
                    PlayerGuid = Guid.Empty,
                    Username = "NOT FOUND"
                };
            }
        }
        
        return new PlayerData()
        {
            PlayerGuid = player.PlayerGuid,
            Username = player.PlayerOocName
        };
    }
    
    /// <summary>
    /// Returns the leaderboard for the most seen players, most antag players, most hunted players, job stats, and more.
    /// </summary>
    /// <param name="rangeOption"> The range of time to get the leaderboard for. </param>
    /// <param name="username"> An optional username to add their position as well. </param>
    [HttpGet]
    [Route("leaderboard")]
    [Authorize(Policy = "TokenBased")]
    public async Task<ActionResult> GetLeaderboard(
        [FromHeader] Guid? accountGuid,
        [FromQuery] RangeOption rangeOption = RangeOption.AllTime,
        [FromQuery] string? username = null
    )
    {
        var requester = await _context.Accounts
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == accountGuid);
        
        requester?.History.Add(new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Shared.Models.Account.Action), Shared.Models.Account.Action.LeaderboardViewed) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = $"Range: {rangeOption}, Username: {username}"
        });
        
        if (requester == null)
        {
            var systemAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Guid == Guid.Empty);
            systemAccount!.History.Add(new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Shared.Models.Account.Action), Shared.Models.Account.Action.LeaderboardViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Range: {rangeOption}, Username: {username}"
            });
        }
        
        await _context.SaveChangesAsync();
        
        var lb = await LeaderboardBackgroundService.Instance.GetLeaderboard(rangeOption, username, accountGuid);
        if (lb.Leaderboards.Count == 0)
        {
            // This can only happen when the requested username has chosen to redact their information, so we hit them with a unauthorized.
            return Unauthorized();
        }

        return Ok(lb);
    }
}
