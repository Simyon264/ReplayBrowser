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
using ReplayBrowser.Models;
using Serilog;
using Action = ReplayBrowser.Data.Models.Account.Action;

namespace ReplayBrowser.Services;

public class LeaderboardService : IHostedService, IDisposable
{
    private static readonly List<string> DepartmentsOrdering = [
        "Command", "Science", "Security", "Medical", "Engineering", "Service", "Cargo", "The tide"
    ];

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

        if (username != null)
        {
            var accountRequested = await context.Accounts
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());

            if (accountRequested != null)
            {
                if (accountRequested.Settings.RedactInformation)
                {
                    if (accountRequested.Id != accountCaller?.Id)
                    {
                        if (accountCaller is not { IsAdmin: true })
                        {
                            throw new UnauthorizedAccessException("This user has chosen to privatize their information.");
                        }
                    }
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
            var player = await context.ReplayParticipants
                .FirstOrDefaultAsync(p => p.Username.ToLower() == username.ToLower());
            if (player != null) usernameGuid = player.PlayerGuid;
        }


        var stopwatch = new Stopwatch();
        long stopwatchPrevious = 0;
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

        var mostplayed = await context.ReplayParticipants
            .Where(p => p.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .GroupBy(p => p.PlayerGuid)
            .Select(pg => new {
                PlayerGuid = pg.Key,
                Count = pg.Select(p => p.ReplayId).Count(),
            })
            .ToListAsync();

        leaderboards["MostSeenPlayers"].Data = mostplayed.ToDictionary(p => p.PlayerGuid.ToString(), p => new PlayerCount {
            Count = p.Count,
            Player = new PlayerData()
            {
                PlayerGuid = p.PlayerGuid,
                Username = string.Empty
            }
        });

        stopwatch.Stop();
        Log.Information("Leaderboard - Most Played done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();

        var mostplayednoghost = await context.ReplayParticipants
            .Where(p => p.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .Where(p => p.Players!.Any(p => p.PlayerIcName != "Unknown"))
            .GroupBy(p => p.PlayerGuid)
            .Select(pg => new {
                PlayerGuid = pg.Key,
                Count = pg.Select(p => p.ReplayId).Count(),
            })
            .ToListAsync();

        leaderboards["MostSeenNoGhost"].Data = mostplayednoghost.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
        {
            Count = x.Count,
            Player = new PlayerData()
            {
                PlayerGuid = x.PlayerGuid,
                Username = string.Empty
            }
        });

        stopwatch.Stop();
        Log.Information("Leaderboard - Most Played No Ghost done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();

        var mostantag = await context.ReplayParticipants
            .Where(p => p.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .Where(p => p.Players!.Any(p => p.AntagPrototypes.Count > 0))
            .GroupBy(p => p.PlayerGuid)
            .Select(pg => new {
                PlayerGuid = pg.Key,
                Count = pg.Select(p => p.ReplayId).Count(),
            })
            .ToListAsync();

        leaderboards["MostAntagPlayers"].Data = mostantag.ToDictionary(x => x.PlayerGuid.ToString(), x => new PlayerCount()
        {
            Count = x.Count,
            Player = new PlayerData()
            {
                PlayerGuid = x.PlayerGuid,
                Username = string.Empty
            }
        });

        stopwatch.Stop();
        Log.Information("Leaderboard - Most Antag done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();

        var mostPlayedDepartments = await context.Players
            .Where(p => p.EffectiveJobId != null)
            .Where(p => p.Participant.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .GroupBy(p => p.EffectiveJob!.Department)
            .Select(pg => new {
                Department = pg.Key,
                Count = pg.Count(),
            })
            .ToListAsync();

        leaderboards["MostPlayedDepartments"].Data = mostPlayedDepartments.ToDictionary(x => x.Department, x => new PlayerCount()
        {
            Count = x.Count,
            Player = new PlayerData()
            {
                PlayerGuid = null,
                Username = x.Department
            }
        });

        stopwatch.Stop();
        Log.Information("Leaderboard - Most Played Department done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();


        var mostPlayedJobs = await context.Players
            .Where(p => p.JobPrototypes.Count > 0)
            .Where(p => p.Participant.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .Select(p => p.JobPrototypes[0])
            .GroupBy(p => p)
            .Select(pg => new {
                Job = pg.Key,
                Count = pg.Count(),
            })
            .ToListAsync();

        leaderboards["MostPlayedJobs"].Data = mostPlayedJobs.ToDictionary(x => x.Job, x => new PlayerCount()
        {
            Count = x.Count,
            Player = new PlayerData()
            {
                PlayerGuid = null,
                Username = x.Job
            }
        });

        stopwatch.Stop();
        Log.Information("Leaderboard - Most Played Job done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();


        var perDepartmentPlayers = await context.Players
            .Where(p => p.EffectiveJobId != null)
            .Where(p => p.Participant.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .GroupBy(p => new {
                p.EffectiveJob!.Department,
                p.Participant.PlayerGuid
            })
            .Select(pg => new {
                pg.Key,
                Count = pg.Count()
            })
            .ToListAsync();

        perDepartmentPlayers
            .GroupBy(g => g.Key.Department, pc => new { pc.Key.PlayerGuid, pc.Count })
            .OrderBy(g => DepartmentsOrdering.IndexOf(g.Key) is var index && index == -1 ? DepartmentsOrdering.Count : index)
            .Select(j => new Leaderboard() {
                Name = j.Key,
                TrackedData = "Times played",
                Data = j.ToDictionary(pc => pc.PlayerGuid.ToString(), pc => new PlayerCount {
                    Count = pc.Count,
                    Player = new PlayerData {
                        PlayerGuid = pc.PlayerGuid,
                        Username = string.Empty
                    }
                })
            })
            .ToList()
            .ForEach(j => leaderboards.Add(j.Name, j));


        stopwatch.Stop();
        Log.Information("Leaderboard - Department done at {TimeTotal}ms (took {Time}ms)", stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds - stopwatchPrevious);
        stopwatchPrevious = stopwatch.ElapsedMilliseconds;
        stopwatch.Start();

        var perJobPlayers = await context.Players
            .Where(p => p.JobPrototypes.Count > 0)
            .Where(p => p.Participant.Replay!.Date >= (DateTime.UtcNow - rangeOption.GetNormalTimeSpan()))
            .GroupBy(p => new {
                Job = p.JobPrototypes[0],
                p.Participant.PlayerGuid
            })
            .Select(pg => new {
                pg.Key,
                Count = pg.Count()
            })
            .ToListAsync();

        perJobPlayers
            .GroupBy(g => g.Key.Job, pc => new { pc.Key.PlayerGuid, pc.Count })
            .Select(j => new Leaderboard() {
                Name = j.Key,
                TrackedData = "Times played",
                Data = j.ToDictionary(pc => pc.PlayerGuid.ToString(), pc => new PlayerCount {
                    Count = pc.Count,
                    Player = new PlayerData {
                        PlayerGuid = pc.PlayerGuid,
                        Username = string.Empty
                    }
                })
            })
            .ToList()
            .ForEach(j => leaderboards.Add(j.Name, j));

        #endregion

        stopwatch.Stop();
        Log.Information("SQL queries took {TimeTotal}ms", stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

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

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        await using var context = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReplayDbContext>();
        foreach (var player in returnValue.Data)
        {
            if (player.Value.Player?.PlayerGuid == null)
                continue;

            // get the latest name from the db
            var playerData = await context.ReplayParticipants
                .Where(p => p.PlayerGuid == player.Value.Player.PlayerGuid)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (playerData == null)
            {
                // ??? try to get using api
                var playerDataApi = await _apiHelper.FetchPlayerDataFromGuid((Guid)player.Value.Player.PlayerGuid);
                if (playerDataApi != null)
                {
                    player.Value.Player.Username = playerDataApi.Username;
                }
            }
            else
            {
                player.Value.Player.Username = playerData.Username;
            }
        }
        stopwatch.Stop();
        Log.Verbose("Fetching player data took {Time}ms", stopwatch.ElapsedMilliseconds);

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