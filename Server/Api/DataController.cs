using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Shared;
using Shared.Models;

namespace Server.Api;

/// <summary>
/// Contains endpoints for data retrieval. Such as search completions, leaderboards, and more.
/// </summary>
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private static readonly Regex HuntedRegex = new Regex(@"Kill(?: or maroon? ([^,]+))");

    private static readonly Dictionary<string, string[]> JobLeaderboards = new Dictionary<string, string[]>()
    {
        {"Command", new []
        {
            "Captain",
            "HeadOfPersonnel",
            "ChiefMedicalOfficer",
            "ResearchDirector",
            "HeadOfSecurity",
            "ChiefEngineer",
            "Quartermaster"
        }},
        {"Science", new []
        {
            "ResearchDirector",
            "Borg",
            "Scientist",
            "ResearchAssistant"
        }},
        {"Security", new []
        {
            "HeadOfSecurity",
            "Warden",
            "Detective",
            "SecurityOfficer",
            "SecurityCadet"
        }},
        {"Medical", new []
        {
            "ChiefMedicalOfficer",
            "MedicalDoctor",
            "Chemist",
            "Paramedic",
            "Psychologist",
            "MedicalIntern"
        }},
        {"Engineering", new []
        {
            "ChiefEngineer",
            "StationEngineer",
            "AtmosphericTechnician",
            "TechnicalAssistant",
        }},
        {"Service", new []
        {
            "HeadOfPersonnel",
            "Janitor",
            "Chef",
            "Botanist",
            "Bartender",
            "Chaplain",
            "Lawyer",
            "Musician",
            "Reporter",
            "Zookeeper",
        }},
        {"Clown & Mime", new []
        {
            "Clown",
            "Mime"
        }},
        {"Cargo", new []
        {
            "Quartermaster",
            "CargoTechnician",
            "SalvageSpecialist",
        }},
        {"The tide", new []
        {
            "Passenger",
        }}
    };
    
    private readonly ReplayDbContext _context;
    private readonly IMemoryCache _cache;
    
    public DataController(ReplayDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    [Route("player-data")]
    public async Task<ActionResult> GetPlayerData(
        [FromQuery] string guid
    )
    {
        var playerGuid = Guid.Parse(guid);
        if (playerGuid == Guid.Empty)
        {
            return BadRequest("Invalid GUID");
        }
        
        var replays = await _context.Players
            .Where(p => p.PlayerGuid == playerGuid)
            .Include(p => p.Replay)
            .Include(r => r.Replay.RoundEndPlayers)
            .Select(p => p.Replay)
            .ToListAsync();

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
                Username = (await FetchPlayerDataFromGuid(playerGuid))?.Username ??
                           "Unable to fetch username (API error)"
            },
            Characters = charactersPlayed,
            TotalEstimatedPlaytime = totalPlaytime,
            TotalRoundsPlayed = totalRoundsPlayed.Count,
            TotalAntagRoundsPlayed = totalAntagRoundsPlayed.Count,
            LastSeen = lastSeen,
            JobCount = jobCount
        };
        
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
    public async Task<PlayerData> HasProfile(
        [FromQuery] string username
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
    public async Task<LeaderboardData> GetLeaderboard(
        [FromQuery] RangeOption rangeOption = RangeOption.AllTime,
        [FromQuery] string? username = null
    )
    {
        // First, try to get the leaderboard from the cache
        var usernameCacheKey = username
            ?.ToLower()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Replace("_", "-");
        if (_cache.TryGetValue("leaderboard-" + rangeOption + "-" + usernameCacheKey, out LeaderboardData leaderboardData))
        {
            return leaderboardData;
        }
        
        var isUsernameProvided = !string.IsNullOrWhiteSpace(username);
        var usernameGuid = Guid.Empty;
        if (isUsernameProvided)
        {
            // Fetch the GUID for the username
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.PlayerOocName.ToLower() == username.ToLower());
            if (player != null) usernameGuid = player.PlayerGuid;
        }
        

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var rangeTimespan = rangeOption.GetTimeSpan();
        var dataReplays = await _context.Replays
            .Where(r => r.Date > DateTime.UtcNow - rangeTimespan)
            .Include(r => r.RoundEndPlayers)
            .ToListAsync();
        
        stopwatch.Stop();
        Log.Information("Fetching replays took {Time}ms", stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
        
        var leaderboards = new Dictionary<string, Leaderboard>()
        {
            {"MostSeenPlayers", new Leaderboard()
            {
                Name = "Most Seen Players",
                TrackedData = "Times seen",
                Data = new Dictionary<string, PlayerCount>()
            }},
            {"MostAntagPlayers", new Leaderboard()
            {
                Name = "Most Antag Players",
                TrackedData = "Times antag",
                Data = new Dictionary<string, PlayerCount>()
            }},
            {"MostHuntedPlayer", new Leaderboard()
            {
                Name = "Most Hunted Player",
                TrackedData = "Times hunted by antags",
                Data = new Dictionary<string, PlayerCount>()
            }}
        };
        
        // Dynamically generate job leaderboards
        foreach (var (job, jobList) in JobLeaderboards)
        {
            leaderboards[job] = new Leaderboard()
            {
                Name = job,
                TrackedData = "Times played in department",
                Data = new Dictionary<string, PlayerCount>(),
                ExtraInfo = "Jobs: " + string.Join(", ", jobList) + "."
            };
        }
        
        // To calculate the most seen player, we just count how many times we see a player in each RoundEndPlayer list.
        // Importantly, we need to filter out in RoundEndPlayers for distinct players since players can appear multiple times there.
        foreach (var dataReplay in dataReplays)
        {
            var distinctBy = dataReplay.RoundEndPlayers.DistinctBy(x => x.PlayerGuid);

            foreach (var player in distinctBy)
            {
                CountUp(player, "MostSeenPlayers", ref leaderboards);
            }

            foreach (var dataReplayRoundEndPlayer in dataReplay.RoundEndPlayers)
            {
                if (dataReplayRoundEndPlayer.Antag)
                {
                    CountUp(dataReplayRoundEndPlayer, "MostAntagPlayers", ref leaderboards);
                }
                
                // Calculate job leaderboards
                foreach (var (job, jobList) in JobLeaderboards)
                {
                    if (jobList.Contains(dataReplayRoundEndPlayer.JobPrototypes.FirstOrDefault()))
                    {
                        CountUp(dataReplayRoundEndPlayer, job, ref leaderboards);
                    }
                }
            }
            
            // The most hunted player is a bit more complex. We need to check the round end text for the following string
            // "Kill or maroon <name>, <job> | "
            // We need to extract the name and then look for that player in the player list for that replay.
            // If we find the player, we increment the count.
            if (dataReplay.RoundEndText == null || dataReplay.RoundEndPlayers == null)
                continue;
            
            var matches = HuntedRegex.Matches(dataReplay.RoundEndText);
            foreach (Match match in matches)
            {
                var playerName = match.Groups[1].Value.Trim();
                var player = dataReplay.RoundEndPlayers.FirstOrDefault(p => p.PlayerIcName == playerName);
                if (player == null)
                    continue;
                
                CountUp(player, "MostHuntedPlayer", ref leaderboards);
            }
        }
        
        // Need to calculate the position of every player in the leaderboard.
        foreach (var leaderboard in leaderboards)
        {
            var leaderboardResult = await GenerateLeaderboard(leaderboard.Key, leaderboard.Key, leaderboard.Value, usernameGuid);
            leaderboards[leaderboard.Key].Data = leaderboardResult.Data;
        }
        
        stopwatch.Stop();
        Log.Information("Calculating leaderboard took {Time}ms", stopwatch.ElapsedMilliseconds);

        // Save leaderboard to cache (its expensive as fuck to calculate)
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(5));
        var cacheLeaderboard = new LeaderboardData()
        {
            Leaderboards = leaderboards.Values.ToList(),
            IsCache = true
        };
        
        _cache.Set("leaderboard-" + rangeOption + "-" + usernameCacheKey, cacheLeaderboard, cacheEntryOptions);

        
        return new LeaderboardData()
        {
            Leaderboards = leaderboards.Values.ToList(),
            IsCache = false
        };
    }

    private void CountUp(Player player, string index, ref Dictionary<string, Leaderboard> data)
    {
        var leaderboard = data[index];
        
        var playerKey = new PlayerData()
        {
            PlayerGuid = player.PlayerGuid,
            Username = ""
        };
        var didAdd = leaderboard.Data.TryAdd(playerKey.PlayerGuid.ToString(), new PlayerCount()
        {
            Count = 1,
            Player = playerKey,
        });
        if (!didAdd)
        {
            leaderboard.Data[playerKey.PlayerGuid.ToString()].Count++;
        }
    }
    
    private async Task<Leaderboard> GenerateLeaderboard(
        string name, 
        string columnName,
        Leaderboard data,
        Guid targetPlayer
        )
    {
        var returnValue = new Leaderboard()
        {
            Name = name,
            TrackedData = columnName,
            Data = new Dictionary<string, PlayerCount>()
        };
        
        var players = data.Data.Values.ToList();
        players.Sort((a, b) => b.Count.CompareTo(a.Count));
        for (var i = 0; i < players.Count; i++)
        {
            players[i].Position = i + 1;
        }
        
        returnValue.Data = players.Take(10).ToDictionary(x => x.Player.PlayerGuid.ToString(), x => x);
        
        if (targetPlayer != Guid.Empty)
        {
            if (!returnValue.Data.ContainsKey(targetPlayer.ToString()))
            {
                returnValue.Data.Add(targetPlayer.ToString(), new PlayerCount()
                {
                    Count = players.FirstOrDefault(x => x.Player.PlayerGuid == targetPlayer)?.Count ?? -1,
                    Player = new PlayerData()
                    {
                        PlayerGuid = targetPlayer,
                        Username = string.Empty
                    },
                    Position = players.FirstOrDefault(x => x.Player.PlayerGuid == targetPlayer)?.Position ?? -1
                });
            }
        }
        
        foreach (var player in returnValue.Data)
        {
            var playerData = await FetchPlayerDataFromGuid(player.Value.Player.PlayerGuid);
            player.Value.Player.Username = playerData.Username;
            await Task.Delay(50); // Rate limit the API
        }
        
        return returnValue;
    }

    private async Task<PlayerData?> FetchPlayerDataFromGuid(Guid guid)
    {
        if (!_cache.TryGetValue(guid.ToString(), out PlayerData? playerKey))
        {
            playerKey = new PlayerData()
            {
                PlayerGuid = guid
            };

            HttpResponseMessage response = null;
            try
            {
                var httpClient = new HttpClient();
                response = await httpClient.GetAsync($"https://central.spacestation14.io/auth/api/query/userid?userid={playerKey.PlayerGuid}");
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var username = JsonSerializer.Deserialize<UsernameResponse>(responseString).userName;
                playerKey.Username = username;
            }
            catch (Exception e)
            {
                Log.Error("Unable to fetch username for player with GUID {PlayerGuid}: {Error}", playerKey.PlayerGuid, e.Message);
                if (e.Message.Contains("'<' is an")) // This is a hacky way to check if we got sent a website.
                {
                    // Probably got sent a website? Log full response.
                    Log.Error("Website might have been sent: {Response}", response?.Content.ReadAsStringAsync().Result);
                }
                
                playerKey.Username = "Unable to fetch username (API error)";
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

            _cache.Set(guid.ToString(), playerKey, cacheEntryOptions);
        }

        return playerKey;
    }
}
