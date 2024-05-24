using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Helpers;
using Serilog;
using Action = ReplayBrowser.Data.Models.Account.Action;

namespace ReplayBrowser.Services;

public class LeaderboardService : IHostedService, IDisposable
{
    private Timer? _timer = null;
    private readonly IMemoryCache _cache;
    private readonly Ss14ApiHelper _apiHelper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccountService _accountService;
    
    public LeaderboardService(IMemoryCache cache, Ss14ApiHelper apiHelper, IServiceScopeFactory factory, AccountService accountService)
    {
        _cache = cache;
        _apiHelper = apiHelper;
        _scopeFactory = factory;
        _accountService = accountService;
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
            var anonymousAuth = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            GetLeaderboard(rangeOption, null, anonymousAuth, false).Wait();
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

    public async Task<LeaderboardData> GetLeaderboard(RangeOption rangeOption, string? username, AuthenticationState authenticationState, bool logAction = true)
    {
        var context = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReplayDbContext>();

        Account? accountCaller = null;
        if (logAction)
        {
            accountCaller = await _accountService.GetAccount(authenticationState);
            await _accountService.AddHistory(accountCaller, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.LeaderboardViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Range: {rangeOption}, Username: {username}"
            });
        }
        
        if (username != null && accountCaller != null)
        {
            var accountRequested = await context.Accounts
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());

            if (accountRequested != null && accountRequested.Settings.RedactInformation &&
                (accountCaller == null || accountCaller.Guid != accountRequested.Guid))
            {
                if (accountCaller == null || !accountCaller.IsAdmin)
                {
                    throw new UnauthorizedAccessException("This user has chosen to privatize their information.");
                }
            }
        }
        
        // First, try to get the leaderboard from the cache
        var usernameCacheKey = username
            ?.ToLower()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Replace("_", "-");
        var cacheKey = "leaderboard-" + rangeOption + "-" + usernameCacheKey;
        if (_cache.TryGetValue(cacheKey, out LeaderboardData leaderboardData))
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
        var leaderboards = new Dictionary<string, Leaderboard>()
        {
            {"MostSeenPlayers", new Leaderboard()
            {
                Name = "Most Seen Players",
                TrackedData = "Times seen",
                Data = new Dictionary<string, PlayerCount>()
            }},
            {"MostSeenNoGhost", new Leaderboard()
            {
                Name = "Most Seen Players Excluding Ghosts",
                TrackedData = "Times seen",
                Data = new Dictionary<string, PlayerCount>()
            }},
            {"MostAntagPlayers", new Leaderboard()
            {
                Name = "Most Antag Players",
                TrackedData = "Times antag",
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

        #region FUNNY SQL QUERIES

        // Running just linq queries is too slow, so we need to run raw SQL queries.
        var mostplayed = await context.Database.SqlQueryRaw<SqlResponse>(
            $"SELECT p.\"PlayerGuid\", p.\"PlayerOocName\", COUNT(DISTINCT p.\"ReplayId\") AS unique_replays_count FROM \"Players\" p JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}' GROUP BY p.\"PlayerGuid\", p.\"PlayerOocName\" ORDER BY unique_replays_count DESC;"
                                                 ).ToListAsync();
        
        leaderboards["MostSeenPlayers"].Data = mostplayed.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
        {
            Count = x.UniqueReplaysCount,
            Player = new PlayerData()
            {
                PlayerGuid = x.PlayerGuid,
                Username = x.PlayerOocName
            }
        });
        
        var mostplayednoghost = await context.Database.SqlQueryRaw<SqlResponse>(
            $"SELECT p.\"PlayerGuid\", p.\"PlayerOocName\", COUNT(DISTINCT p.\"ReplayId\") AS unique_replays_count FROM \"Players\" p JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE p.\"PlayerIcName\" != 'Unknown' AND r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}' GROUP BY p.\"PlayerGuid\", p.\"PlayerOocName\" ORDER BY unique_replays_count DESC;"
        ).ToListAsync();
        
        leaderboards["MostSeenNoGhost"].Data = mostplayednoghost.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
        {
            Count = x.UniqueReplaysCount,
            Player = new PlayerData()
            {
                PlayerGuid = x.PlayerGuid,
                Username = x.PlayerOocName
            }
        });
        
        var mostantag = await context.Database.SqlQueryRaw<SqlResponse>(
           $"SELECT p.\"PlayerGuid\", p.\"PlayerOocName\", COUNT(p.\"ReplayId\") AS unique_replays_count FROM \"Players\" p JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE array_length(p.\"AntagPrototypes\", 1) > 0 AND r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}' GROUP BY p.\"PlayerGuid\", p.\"PlayerOocName\" ORDER BY unique_replays_count DESC;"
        ).ToListAsync();
        
        leaderboards["MostAntagPlayers"].Data = mostantag.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
        {
            Count = x.UniqueReplaysCount,
            Player = new PlayerData()
            {
                PlayerGuid = x.PlayerGuid,
                Username = x.PlayerOocName
            }
        });
        
        var mostPlayedDepartments = await context.Database.SqlQueryRaw<DepartmentSqlResponse>(
            $"WITH role_to_department AS( SELECT * FROM (VALUES ('Captain', 'Command'), ('HeadOfPersonnel', 'Command'), ('ChiefMedicalOfficer', 'Command'), ('ResearchDirector', 'Command'), ('HeadOfSecurity', 'Command'), ('ChiefEngineer', 'Command'), ('Quartermaster', 'Command'), ('Borg', 'Science'), ('Scientist', 'Science'), ('ResearchAssistant', 'Science'), ('Warden', 'Security'), ('Detective', 'Security'), ('SecurityOfficer', 'Security'), ('SecurityCadet', 'Security'), ('MedicalDoctor', 'Medical'), ('Chemist', 'Medical'), ('Paramedic', 'Medical'), ('Psychologist', 'Medical'), ('MedicalIntern', 'Medical'), ('StationEngineer', 'Engineering'), ('AtmosphericTechnician', 'Engineering'), ('TechnicalAssistant', 'Engineering'), ('Janitor', 'Service'), ('Chef', 'Service'), ('Botanist', 'Service'), ('Bartender', 'Service'), ('Chaplain', 'Service'), ('Lawyer', 'Service'), ('Musician', 'Service'), ('Reporter', 'Service'), ('Zookeeper', 'Service'), ('Librarian', 'Service'), ('ServiceWorker', 'Service'), ('Clown', 'Service'), ('Mime', 'Service'), ('CargoTechnician', 'Cargo'), ('SalvageSpecialist', 'Cargo'), ('Passenger', 'The tide')) AS mapping(role, department) ) SELECT mapping.department, COUNT(*) AS department_count FROM \"Players\" p JOIN role_to_department mapping ON mapping.role = ANY(p.\"JobPrototypes\") JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}' GROUP BY mapping.department ORDER BY department_count DESC; "
        ).ToListAsync();
        
        leaderboards["MostPlayedDepartments"].Data = mostPlayedDepartments.ToDictionary(x => x.Department, x => new PlayerCount()
        {
            Count = x.DepartmentCount,
            Player = new PlayerData()
            {
                PlayerGuid = null,
                Username = x.Department
            }
        });

        var mostPlayedJobs = await context.Database.SqlQueryRaw<JobLeaderboardSqlResponse>(
            $"SELECT job, COUNT(*) AS job_count FROM( SELECT UNNEST(p.\"JobPrototypes\") AS job FROM \"Players\" p JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}') AS jobs GROUP BY job ORDER BY job_count DESC; "
        ).ToListAsync();
        
        leaderboards["MostPlayedJobs"].Data = mostPlayedJobs.ToDictionary(x => x.Job, x => new PlayerCount()
        {
            Count = x.JobCount,
            Player = new PlayerData()
            {
                PlayerGuid = null,
                Username = x.Job
            }
        });
        
        // For each role found in the job leaderboards, we add another leaderboard for that role, showing the players who played it the most
        var jobs = mostPlayedJobs.Select(job => job.Job).ToList();
        jobs.Sort();
        
        foreach (var jobName in jobs)
        {
            var jobPlayers = await context.Database.SqlQueryRaw<SqlResponse>(
                $"SELECT p.\"PlayerGuid\", p.\"PlayerOocName\", COUNT(p.\"ReplayId\") AS unique_replays_count FROM \"Players\" p JOIN \"Replays\" r ON p.\"ReplayId\" = r.\"Id\" WHERE '{jobName}' = ANY(p.\"JobPrototypes\") AND r.\"Date\" >= CURRENT_DATE - INTERVAL '{rangeTimespan}' GROUP BY p.\"PlayerGuid\", p.\"PlayerOocName\" ORDER BY unique_replays_count DESC;"
            ).ToListAsync();
            
            leaderboards[jobName] = new Leaderboard()
            {
                Name = jobName,
                TrackedData = "Times played",
                Data = jobPlayers.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
                {
                    Count = x.UniqueReplaysCount,
                    Player = new PlayerData()
                    {
                        PlayerGuid = x.PlayerGuid,
                        Username = x.PlayerOocName
                    }
                })
            };
        }
        
        #endregion
        
        // Need to calculate the position of every player in the leaderboard.
        foreach (var leaderboard in leaderboards)
        {
            var leaderboardResult = await GenerateLeaderboard(leaderboard.Key, leaderboard.Key, leaderboard.Value, usernameGuid, leaderboard.Value.Limit);
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
        
        _cache.Set(cacheKey, cacheLeaderboard, cacheEntryOptions);

        
        return new LeaderboardData()
        {
            Leaderboards = leaderboards.Values.ToList(),
            IsCache = false
        };
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
    
    private class SqlResponse
    {
        [Column("PlayerGuid")]
        public Guid PlayerGuid { get; set; }
        [Column("PlayerOocName")]
        public string PlayerOocName { get; set; }
        [Column("unique_replays_count")]
        public int UniqueReplaysCount { get; set; }
    }

    private class DepartmentSqlResponse
    {
        [Column("department")]
        public string Department { get; set; }
        [Column("department_count")]
        public int DepartmentCount { get; set; }
    }
    
    private class JobLeaderboardSqlResponse
    {
        [Column("job")]
        public string Job { get; set; }
        [Column("job_count")]
        public int JobCount { get; set; }
    }
}