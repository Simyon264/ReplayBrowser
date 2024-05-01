using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Shared;

namespace Server.Api;

/// <summary>
/// Contains endpoints for data retrieval. Such as search completions, leaderboards, and more.
/// </summary>
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    public static readonly Regex HuntedRegex = new Regex(@"Kill(?: or maroon? ([^,]+))");

    private readonly ReplayDbContext _context;
    private readonly IMemoryCache _cache;
    
    public static readonly Dictionary<Guid, WebSocket> ConnectedUsers = new();
    private Timer _timer;
    
    public DataController(ReplayDbContext context, IMemoryCache cache)
    {
        _context = context;
        _timer = new Timer(CheckInactiveConnections, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
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
        var totalRoundsPlayed = 0;
        var totalAntagRoundsPlayed = 0;
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
            
            totalRoundsPlayed++;
            totalAntagRoundsPlayed += replay.RoundEndPlayers.Any(p => p.PlayerGuid == playerGuid && p.Antag) ? 1 : 0; // If the player is an antag, increment the count.
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
            TotalRoundsPlayed = totalRoundsPlayed,
            TotalAntagRoundsPlayed = totalAntagRoundsPlayed,
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
    
    [HttpGet]
    [Route("leaderboard")]
    public async Task<LeaderboardData> GetLeaderboard(
        [FromQuery] RangeOption rangeOption = RangeOption.AllTime
    )
    {
        // First, try to get the leaderboard from the cache
        if (_cache.TryGetValue("leaderboard-" + rangeOption, out LeaderboardData leaderboardData))
        {
            return leaderboardData;
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

        var leaderboardResult = new LeaderboardData()
        {
            MostSeenPlayers = new Dictionary<string, PlayerCount>(),
            MostAntagPlayers = new Dictionary<string, PlayerCount>(),
            MostHuntedPlayer = new Dictionary<string, PlayerCount>(),
        };
        
        // To calculate the most seen player, we just count how many times we see a player in each RoundEndPlayer list.
        // Importantly, we need to filter out in RoundEndPlayers for distinct players since players can appear multiple times there.
        foreach (var dataReplay in dataReplays)
        {
            var distinctBy = dataReplay.RoundEndPlayers.DistinctBy(x => x.PlayerGuid);

            #region Most seen

            foreach (var player in distinctBy)
            {
                var playerKey = new PlayerData()
                {
                    PlayerGuid = player.PlayerGuid,
                    Username = "" // Will be filled in later (god im so sorry PJB)
                };

                var didAdd = leaderboardResult.MostSeenPlayers.TryAdd(playerKey.PlayerGuid.ToString(), new PlayerCount()
                {
                    Count = 1,
                    Player = playerKey,
                });
                if (!didAdd)
                {
                    // If the player already exists in the dictionary, we just increment the count.
                    leaderboardResult.MostSeenPlayers[playerKey.PlayerGuid.ToString()].Count++;
                }
            }

            #endregion
            
            #region Most seen as antag

            foreach (var dataReplayRoundEndPlayer in dataReplay.RoundEndPlayers)
            {
                if (!dataReplayRoundEndPlayer.Antag)
                    continue;
                
                var playerKey = new PlayerData()
                {
                    PlayerGuid = dataReplayRoundEndPlayer.PlayerGuid,
                    Username = ""
                };
                var didAdd = leaderboardResult.MostAntagPlayers.TryAdd(playerKey.PlayerGuid.ToString(), new PlayerCount()
                {
                    Player = playerKey,
                    Count = 1,
                });
                if (!didAdd)
                {
                    leaderboardResult.MostAntagPlayers[playerKey.PlayerGuid.ToString()].Count++;
                }
            }

            #endregion

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
                
                var playerKey = new PlayerData()
                {
                    PlayerGuid = player.PlayerGuid,
                    Username = ""
                };
                var didAdd = leaderboardResult.MostHuntedPlayer.TryAdd(playerKey.PlayerGuid.ToString(), new PlayerCount()
                {
                    Count = 1,
                    Player = playerKey,
                });
                if (!didAdd)
                {
                    leaderboardResult.MostHuntedPlayer[playerKey.PlayerGuid.ToString()].Count++;
                }
            }
        }
        
        // Need to only return the top 10 players
        leaderboardResult.MostSeenPlayers = leaderboardResult.MostSeenPlayers
            .OrderByDescending(p => p.Value.Count)
            .Take(10)
            .ToDictionary(p => p.Key, p => p.Value);
        
        leaderboardResult.MostAntagPlayers = leaderboardResult.MostAntagPlayers
            .OrderByDescending(p => p.Value.Count)
            .Take(10)
            .ToDictionary(p => p.Key, p => p.Value);
        
        leaderboardResult.MostHuntedPlayer = leaderboardResult.MostHuntedPlayer
            .OrderByDescending(p => p.Value.Count)
            .Take(10)
            .ToDictionary(p => p.Key, p => p.Value);
        
        stopwatch.Stop();
        Log.Information("Calculating leaderboard took {Time}ms", stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
        
        // Now we need to fetch the usernames for the players
        foreach (var player in leaderboardResult.MostSeenPlayers)
        {
            var playerData = await FetchPlayerDataFromGuid(player.Value.Player.PlayerGuid);
            player.Value.Player.Username = playerData.Username;
            await Task.Delay(50); // Rate limit the API
        }

        foreach (var player in leaderboardResult.MostAntagPlayers)
        {
            var playerData = await FetchPlayerDataFromGuid(player.Value.Player.PlayerGuid);
            player.Value.Player.Username = playerData.Username;
            await Task.Delay(50); // Rate limit the API
        }
        
        foreach (var player in leaderboardResult.MostHuntedPlayer)
        {
            var playerData = await FetchPlayerDataFromGuid(player.Value.Player.PlayerGuid);
            player.Value.Player.Username = playerData.Username;
            await Task.Delay(50); // Rate limit the API
        }
        
        stopwatch.Stop();
        Log.Information("Fetching usernames took {Time}ms", stopwatch.ElapsedMilliseconds);
        
        // Save leaderboard to cache (its expensive as fuck to calculate)
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(5));
        var cacheLeaderboard = leaderboardResult;
        cacheLeaderboard.IsCache = true;
        
        _cache.Set("leaderboard-" + rangeOption, cacheLeaderboard, cacheEntryOptions);

        
        return leaderboardResult;
    }
    
    [HttpGet] // this is kind of stupid? swagger does not work without having a method identifier or something
    [Route("/ws")]
    public async Task Connect()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var userId = Guid.NewGuid();
            ConnectedUsers.Add(userId, webSocket);
            Log.Information("User connected with ID {UserId}", userId);
            await Echo(webSocket, userId);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
    
    private async Task Echo(WebSocket webSocket, Guid userId)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (Encoding.UTF8.GetString(buffer).Contains("count"))
            {
                var count = ConnectedUsers.Count;
                var countBytes = Encoding.UTF8.GetBytes(count.ToString());
                await webSocket.SendAsync(new ArraySegment<byte>(countBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            buffer = new byte[1024 * 4];
        }

        ConnectedUsers.Remove(userId, out _);
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
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
    
    private void CheckInactiveConnections(object state)
    {
        foreach (var user in ConnectedUsers)
        {
            if (user.Value.State == WebSocketState.Open) continue;
            
            ConnectedUsers.Remove(user.Key, out _);
            Log.Information("User disconnected with ID {UserId}", user.Key);
        }
    }
}