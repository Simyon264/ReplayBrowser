﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq.Expressions;
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
    private readonly Ss14ApiHelper _apiHelper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccountService _accountService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// How long a leaderboard will be kept valid in the DB before it is updated.
    /// </summary>
    private static readonly TimeSpan MaxCacheTime = TimeSpan.FromHours(1);

    public static bool IsUpdating { get; private set; } = false;
    public static DateTime UpdateStarted { get; private set; } = DateTime.MinValue;
    public static int UpdateProgress { get; private set; } = 0;
    public static int UpdateTotal { get; private set; } = 0;

    public LeaderboardService(Ss14ApiHelper apiHelper, IServiceScopeFactory factory, AccountService accountService, IConfiguration configuration)
    {
        _apiHelper = apiHelper;
        _scopeFactory = factory;
        _accountService = accountService;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(24));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        if (IsUpdating)
        {
            Log.Warning("Leaderboard update already in progress, skipping this update.");
            return;
        }

        IsUpdating = true;
        UpdateStarted = DateTime.UtcNow;

        var sw = new Stopwatch();
        sw.Start();
        Log.Information("Updating leaderboards...");

        var servers = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>()!.Select(x => x.FallBackServerName)
            .Distinct().ToList();

        UpdateTotal = Enum.GetValues<RangeOption>().Length;

        var values = Enum.GetValues<RangeOption>();
        foreach (var rangeOption in values)
        {
            try
            {
                await GenerateLeaderboard(rangeOption, servers.ToArray());
            }
            finally
            {
                UpdateProgress++;
            }
        }

        sw.Stop();
        Log.Information("Leaderboards updated in {Time}", sw.Elapsed);

        IsUpdating = false;
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

    public async Task<IEnumerable<Leaderboard>?> GetLeaderboards(RangeOption rangeOption, string? username, string[]? servers,
        AuthenticationState authenticationState, int entries = 10, bool logAction = true)
    {
        if (servers == null || servers.Length == 0)
        {
            servers = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>()!.Select(x => x.FallBackServerName).ToArray();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        Account? accountCaller = null;
        if (logAction)
        {
            accountCaller = await _accountService.GetAccount(authenticationState);
            await _accountService.AddHistory(accountCaller, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.LeaderboardViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Range: {rangeOption}, Username: {username}, Servers: {string.Join(", ", servers)}"
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

        var redactedAccounts = await context.Accounts
            .Where(a => a.Settings.RedactInformation)
            .Select(a => a.Guid)
            .ToListAsync();

        var entriesToGenerate = entries + redactedAccounts.Count; // Add the redacted accounts to the count so that removed listings still show the correct amount of entries

        var lastUpdate = await context.Leaderboards
            .Where(l => l.Servers.SequenceEqual(servers))
            .Where(l => l.RangeOption == rangeOption)
            .OrderByDescending(l => l.GeneratedAt)
            .Take(1)
            .Select(l => l.GeneratedAt)
            .FirstOrDefaultAsync();

        if (lastUpdate < DateTime.UtcNow - MaxCacheTime)
        {
            if (IsUpdating)
            {
                return [];
            }
            await GenerateLeaderboard(rangeOption, servers);
        }

        var leaderboards = await context.Leaderboards
            .Where(l => l.Servers.SequenceEqual(servers))
            .Where(l => l.RangeOption == rangeOption)
            .Where(l => l.Position <= entriesToGenerate || l.Username == username)
            .Include(l => l.LeaderboardDefinition)
            .ToListAsync();

        var finalReturned = new Dictionary<string, Leaderboard>();

        foreach (var position in leaderboards)
        {
            // Remove positions that are redacted
            if (position.PlayerGuid != null && redactedAccounts.Contains((Guid)position.PlayerGuid))
            {
                continue;
            }

            if (!finalReturned.ContainsKey(position.LeaderboardDefinition.Name))
            {
                finalReturned.Add(position.LeaderboardDefinition.Name, new Leaderboard()
                {
                    Name = position.LeaderboardDefinition.Name,
                    TrackedData = position.LeaderboardDefinition.TrackedData,
                    Data = new Dictionary<string, PlayerCount>()
                });
            }

            finalReturned[position.LeaderboardDefinition.Name].Data[position.PlayerGuid.ToString() ?? GenerateRandomGuid().ToString()] = new PlayerCount()
            {
                Count = position.Count,
                Player = new PlayerData()
                {
                    PlayerGuid = position.PlayerGuid,
                    Username = position.Username
                },
                Position = position.Position
            };
        }

        var returnList = new List<Leaderboard>();
        foreach (var (key, value) in finalReturned)
        {
            returnList.Add(await FinalizeLeaderboard(key, value.NameColumn, value, accountCaller?.Guid ?? Guid.Empty, authenticationState, entries));
        }

        return returnList;
    }

    public async Task GenerateLeaderboard(RangeOption rangeOption, string[]? servers)
    {
        if (servers == null || servers.Length == 0)
        {
            servers = _configuration.GetSection("ReplayUrls").Get<StorageUrl[]>()!.Select(x => x.FallBackServerName).ToArray();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        // Don't update if it's already been updated recently
        var lastUpdate = await context.Leaderboards
            .Where(l => l.Servers.SequenceEqual(servers))
            .Where(l => l.RangeOption == rangeOption)
            .OrderByDescending(l => l.GeneratedAt)
            .Take(1)
            .Select(l => l.GeneratedAt)
            .FirstOrDefaultAsync();

        if (lastUpdate >= DateTime.UtcNow - MaxCacheTime)
        {
            Log.Information("Leaderboards already updated recently, skipping update.");
            return;
        }

        var stopwatch = new Stopwatch();
        long stopwatchPrevious = 0;
        stopwatch.Start();
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
            .Where(p => servers.Contains(p.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Participant.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Participant.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Participant.Replay.ServerName))
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
            .Where(p => servers.Contains(p.Participant.Replay.ServerName))
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

        // Set the positions
        foreach (var leaderboard in leaderboards)
        {
            var players = leaderboard.Value.Data.Values.ToList();
            players.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (var i = 0; i < players.Count; i++)
            {
                players[i].Position = i + 1;
            }
        }

        var generatedAt = DateTime.UtcNow;

        var dbLeaderboards = new List<LeaderboardPosition>();
        foreach (var (key, leaderboard) in leaderboards)
        {
            // Ensure a leaderboard definition exists
            var leaderboardDefinition = await context.LeaderboardDefinitions
                .FirstOrDefaultAsync(l => l.Name == key);

            if (leaderboardDefinition == null)
            {
                leaderboardDefinition = new LeaderboardDefinition()
                {
                    Name = key,
                    TrackedData = leaderboard.TrackedData,
                    NameColumn = leaderboard.NameColumn,
                    ExtraInfo = leaderboard.ExtraInfo
                };
                await context.LeaderboardDefinitions.AddAsync(leaderboardDefinition);
            } else
            {
                leaderboardDefinition.TrackedData = leaderboard.TrackedData;
                leaderboardDefinition.NameColumn = leaderboard.NameColumn;
                leaderboardDefinition.ExtraInfo = leaderboard.ExtraInfo;
            }

            await context.SaveChangesAsync();

            foreach (var (s, value) in leaderboard.Data)
            {
                if (string.IsNullOrEmpty(value.Player?.Username) && value.Player?.PlayerGuid != null)
                {
                    value.Player.Username = await GetNameFromDbOrApi((Guid)value.Player.PlayerGuid);
                }

                // S is player guid
                dbLeaderboards.Add(new LeaderboardPosition()
                {
                    Servers = servers.ToList(),
                    Count = value.Count,
                    PlayerGuid = value.Player?.PlayerGuid,
                    Username = value.Player?.Username ?? string.Empty,
                    LeaderboardDefinitionName = key,
                    GeneratedAt = generatedAt,
                    Position = value.Position,
                    RangeOption = rangeOption
                });
            }
        }

        stopwatch.Restart();

        var serverSet = new HashSet<string>(servers);
        Expression<Func<string, bool>> serverSetContains = server => serverSet.Contains(server);

        context.Leaderboards.RemoveRange(
            context.Leaderboards.Where(l => l.Servers.AsQueryable().All(serverSetContains) && l.Servers.Count == serverSet.Count && l.RangeOption == rangeOption));
        Log.Information("Removing old leaderboards took {Time}ms", stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
        await context.Leaderboards.AddRangeAsync(dbLeaderboards);
        Log.Information("Adding new leaderboards took {Time}ms", stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
        await context.SaveChangesAsync();
        Log.Information("Saving leaderboards to database took {Time}ms", stopwatch.ElapsedMilliseconds);
    }

    private async Task<Leaderboard> FinalizeLeaderboard(
        string name,
        string columnName,
        Leaderboard data,
        Guid targetPlayer,
        AuthenticationState authenticationState,
        int limit = 10
    )
    {
        var returnValue = new Leaderboard()
        {
            Name = name,
            TrackedData = columnName,
            Data = new Dictionary<string, PlayerCount>()
        };

        var redactedAccounts = await _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReplayDbContext>().Accounts
            .Where(a => a.Settings.RedactInformation)
            .Select(a => a.Guid)
            .ToListAsync();

        var account = await _accountService.GetAccount(authenticationState);
        // Remove any redacted accounts
        var players = data.Data.Values.ToList();
        if (account == null || !account.IsAdmin)
        {
            players = players.Where(p =>
                p.Player?.PlayerGuid == null
                || (
                    !redactedAccounts.Contains((Guid)p.Player.PlayerGuid)
                    && account?.Guid != p.Player.PlayerGuid // Users can see their own data even if redacted
                    ))
                .ToList();
        }


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
                    Count = players.FirstOrDefault(x => x.Player?.PlayerGuid == targetPlayer)?.Count ?? -1,
                    Player = new PlayerData()
                    {
                        PlayerGuid = targetPlayer,
                        Username = string.Empty
                    },
                    Position = players.FirstOrDefault(x => x.Player?.PlayerGuid == targetPlayer)?.Position ?? -1
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

            player.Value.Player.Username = await GetNameFromDbOrApi((Guid)player.Value.Player.PlayerGuid);
        }
        stopwatch.Stop();
        Log.Verbose("Fetching player data took {Time}ms", stopwatch.ElapsedMilliseconds);

        return returnValue;
    }

    private async Task<string> GetNameFromDbOrApi(Guid guid)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        // get the latest name from the db
        var playerData = await context.ReplayParticipants
            .Where(p => p.PlayerGuid == guid)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        if (playerData != null) return playerData.Username;

        // ??? try to get using api
        var playerDataApi = await _apiHelper.FetchPlayerDataFromGuid(guid);
        return playerDataApi.Username;

    }

    private Guid GenerateRandomGuid()
    {
        var guidBytes = new byte[16];
        new Random().NextBytes(guidBytes);
        return new Guid(guidBytes);
    }
}