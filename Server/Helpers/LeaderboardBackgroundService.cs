using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Server.Api;
using Shared;
using Shared.Models;

namespace Server.Helpers;

public class LeaderboardBackgroundService : IHostedService, IDisposable
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
    
    private Timer? _timer = null;
    private readonly IMemoryCache _cache;
    private readonly Ss14ApiHelper _apiHelper;
    private readonly IServiceScopeFactory _scopeFactory;

    public static LeaderboardBackgroundService Instance { get; private set; }
    
    public LeaderboardBackgroundService(IMemoryCache cache, Ss14ApiHelper apiHelper, IServiceScopeFactory factory)
    {
        _cache = cache;
        _apiHelper = apiHelper;
        _scopeFactory = factory;
        Instance = this;
    }
    
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(6));
        return Task.CompletedTask;
    }
    
    private void DoWork(object? state)
    {
        var sw = new Stopwatch();
        sw.Start();
        Log.Information("Updating leaderboards...");
        // Loop through every range option.
        foreach (var rangeOption in Enum.GetValues<RangeOption>())
        {
            GetLeaderboard(rangeOption, null, null).Wait();
        }
        
        sw.Stop();
        Log.Information("Leaderboards updated in {Time}", sw.Elapsed);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async Task<LeaderboardData> GetLeaderboard(RangeOption rangeOption, string? username, Guid? accountGuid)
    {
        var context = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        var accountCaller = await context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Guid == accountGuid);

        if (username != null)
        {
            var accountRequested = await context.Accounts
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());

            if (accountRequested != null && accountRequested.Settings.RedactInformation &&
                (accountCaller == null || accountCaller.Guid != accountRequested.Guid))
            {
                if (accountCaller == null || !accountCaller.IsAdmin)
                {
                    return new LeaderboardData()
                    {
                        Leaderboards = new List<Leaderboard>(),
                        IsCache = false
                    };
                }
            }
        }
        
        // First, try to get the leaderboard from the cache
        var usernameCacheKey = username
            ?.ToLower()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Replace("_", "-");
        if (_cache.TryGetValue("leaderboard-" + rangeOption + "-" + usernameCacheKey + "-" + accountGuid, out LeaderboardData leaderboardData))
        {
            return leaderboardData;
        }
        
        var isUsernameProvided = !string.IsNullOrWhiteSpace(username);
        var usernameGuid = Guid.Empty;
        if (isUsernameProvided)
        {
            // Fetch the GUID for the username
            var player = await context.Players
                .FirstOrDefaultAsync(p => p.PlayerOocName.ToLower() == username.ToLower());
            if (player != null) usernameGuid = player.PlayerGuid;
        }
        

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var rangeTimespan = rangeOption.GetTimeSpan();

        var replaysCacheKey = "replays-" + rangeOption;
        if (!_cache.TryGetValue(replaysCacheKey, out List<Replay> dataReplays))
        {
            dataReplays = await context.Replays
                .Where(r => r.Date > DateTime.UtcNow - rangeTimespan)
                .Include(r => r.RoundEndPlayers)
                .ToListAsync();
            _cache.Set(replaysCacheKey, dataReplays, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(4.5f)));
        }
        
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
            }},
            {"MostPlayedDepartments", new Leaderboard()
            {
                Name = "Most played departments",
                TrackedData = "Times played",
                Data = new Dictionary<string, PlayerCount>(),
                Limit = int.MaxValue,
                NameColumn = "Department"
            }},
            {"MostPlayedJobs", new Leaderboard()
            {
                Name = "Most played jobs",
                TrackedData = "Times played",
                Data = new Dictionary<string, PlayerCount>(),
                Limit = int.MaxValue,
                NameColumn = "Job"
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
        
        var mostPlayedDepartments = new Dictionary<string, int>();
        var mostPlayedJobs = new Dictionary<string, int>();
        
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
                        
                        if (!mostPlayedDepartments.TryAdd(job, 1))
                        {
                            mostPlayedDepartments[job]++;
                        }
                    }

                    if (!mostPlayedJobs.TryAdd(dataReplayRoundEndPlayer.JobPrototypes.FirstOrDefault() ?? "Unknown", 1))
                    {
                        mostPlayedJobs[dataReplayRoundEndPlayer.JobPrototypes.FirstOrDefault() ?? "Unknown"]++;
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
        
        // Add most played departments to the leaderboard
        foreach (var (department, count) in mostPlayedDepartments)
        {
            var didAdd = leaderboards["MostPlayedDepartments"].Data.TryAdd(department, new PlayerCount()
            {
                Count = count,
                Player = new PlayerData() { PlayerGuid = null, Username = department }
                // no player data for departments
            });
            if (!didAdd)
            {
                leaderboards["MostPlayedDepartments"].Data[department].Count++;
            }
        }
        
        // Add most played jobs to the leaderboard
        foreach (var (job, count) in mostPlayedJobs)
        {
            var didAdd = leaderboards["MostPlayedJobs"].Data.TryAdd(job, new PlayerCount()
            {
                Count = count,
                Player = new PlayerData() { PlayerGuid = null, Username = job }
                // no player data for jobs
            });
            if (!didAdd)
            {
                leaderboards["MostPlayedJobs"].Data[job].Count++;
            }
        }
        
        // Need to calculate the position of every player in the leaderboard.
        foreach (var leaderboard in leaderboards)
        {
            var leaderboardResult = await GenerateLeaderboard(leaderboard.Key, leaderboard.Key, leaderboard.Value, usernameGuid, leaderboard.Value.Limit);
            leaderboards[leaderboard.Key].Data = leaderboardResult.Data;
        }
        
        stopwatch.Stop();
        Log.Information("Calculating leaderboard took {Time}ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        // Redact usernames for redacted players
        foreach (var leaderboard in leaderboards)
        {
            foreach (var player in leaderboard.Value.Data)
            {
                if (player.Value.Player?.PlayerGuid == null)
                    continue;
                var account = context.Accounts
                    .Include(a => a.Settings)
                    .FirstOrDefault(a => a.Guid == player.Value.Player.PlayerGuid);
                
                if (account == null)
                    continue;
                
                if (account.Settings.RedactInformation && (accountCaller == null || accountCaller.Guid != account.Guid))
                {
                    player.Value.Player.RedactInformation();
                }
            }
        }
        
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
        Guid targetPlayer,
        int limit = 10
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
        
        returnValue.Data = players.Take(limit).ToDictionary(x => x.Player?.PlayerGuid != null ? x.Player.PlayerGuid.ToString()! : GenerateRandomGuid().ToString(), x => x);
        
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
            if (player.Value.Player?.PlayerGuid == null) 
                continue;
            var playerData = await _apiHelper.FetchPlayerDataFromGuid((Guid)player.Value.Player.PlayerGuid);
            player.Value.Player.Username = playerData.Username;
            await Task.Delay(50); // Rate limit the API
        }
        
        return returnValue;
    }
    
    private Guid GenerateRandomGuid()
    {
        var guidBytes = new byte[16];
        new Random().NextBytes(guidBytes);
        return new Guid(guidBytes);
    }
}