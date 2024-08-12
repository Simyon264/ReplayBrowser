using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Models;
using ReplayBrowser.Services;
using Serilog;
using Action = ReplayBrowser.Data.Models.Account.Action;

namespace ReplayBrowser.Helpers;

public class ReplayHelper
{
    private readonly IMemoryCache _cache;
    private readonly ReplayDbContext _context;
    private readonly AccountService _accountService;
    private readonly Ss14ApiHelper _apiHelper;

    public ReplayHelper(IMemoryCache cache, ReplayDbContext context, AccountService accountService, Ss14ApiHelper apiHelper)
    {
        _cache = cache;
        _context = context;
        _accountService = accountService;
        _apiHelper = apiHelper;
    }

    public async Task<List<ReplayResult>> GetMostRecentReplays(AuthenticationState state)
    {
        var replays = await _context.Replays
            .AsNoTracking()
            .OrderByDescending(r => r.Date)
            .Take(32)
            .Select(r => r.ToResult())
            .ToListAsync();

        var account = await _accountService.GetAccount(state);

        // Log the action in a separate task to not block the request.
        Task.Run(async () =>
        {
            await _accountService.AddHistory(account, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.MainPageViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = string.Empty
            });
        });

        return replays;
    }

    /// <summary>
    /// Fetches a player profile from the database.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the account is private and the requestor is not the account owner or an admin.</exception>
    public async Task<CollectedPlayerData?> GetPlayerProfile(Guid playerGuid, AuthenticationState authenticationState, bool skipPermsCheck = false)
    {
        var accountCaller = await _accountService.GetAccount(authenticationState);

        var isGdpr = _context.GdprRequests.Any(g => g.Guid == playerGuid);
        if (isGdpr)
        {
            throw new UnauthorizedAccessException("This account is protected by a GDPR request. There is no data available.");
        }

        var accountRequested = _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefault(a => a.Guid == playerGuid);

        if (!skipPermsCheck)
        {
            if (accountRequested is { Settings.RedactInformation: true })
            {
                if (accountCaller == null || !accountCaller.IsAdmin)
                {
                    if (accountCaller?.Guid != playerGuid)
                    {
                        if (accountRequested.Protected)
                        {
                            throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                        }
                        else
                        {
                            throw new UnauthorizedAccessException(
                                "The account you are trying to view is private. Contact the account owner and ask them to make their account public.");
                        }
                    }
                }
            }
        }

        if (!skipPermsCheck)
        {
            await _accountService.AddHistory(accountCaller, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.ProfileViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Player GUID: {playerGuid} Username: {(await _apiHelper.FetchPlayerDataFromGuid(playerGuid)).Username ?? "Unknown"}"
            });
        }

        // first check for the db cache
        if (_context.PlayerProfiles.Any(p => p.PlayerGuid == playerGuid) && !skipPermsCheck)
        {
            var profile = await _context.PlayerProfiles
                .Include(p => p.Characters)
                .Include(p => p.JobCount)
                .Include(p => p.PlayerData)
                .FirstOrDefaultAsync(p => p.PlayerGuid == playerGuid);

            if (profile != null)
            {
                profile.IsWatched = accountCaller?.SavedProfiles.Contains(playerGuid) ?? false;

                return profile;
            }
        }

        var replays = await _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundEndPlayers)
            .Where(r => r.RoundEndPlayers != null)
            .Where(r => r.RoundEndPlayers!.Any(p => p.PlayerGuid == playerGuid))
            .Distinct() // only need one instance of each replay
            .ToListAsync();

        var charactersPlayed = new List<CharacterData>();
        var totalPlaytime = TimeSpan.Zero;
        var totalRoundsPlayed = new List<int>();
        var totalAntagRoundsPlayed = new List<int>();
        var lastSeen = DateTime.MinValue;
        var jobCount = new List<JobCountData>();

        foreach (var replay in replays)
        {
            if (replay.RoundEndPlayers == null)
                continue;

            if (replay.Date == null)
            {
                Log.Warning("Replay with ID {ReplayId} has no date", replay.Id);
                continue;
            }

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
            PlayerGuid = playerGuid,
            Characters = charactersPlayed,
            TotalEstimatedPlaytime = totalPlaytime,
            TotalRoundsPlayed = totalRoundsPlayed.Count,
            TotalAntagRoundsPlayed = totalAntagRoundsPlayed.Count,
            LastSeen = lastSeen,
            JobCount = jobCount,
            GeneratedAt = DateTime.UtcNow
        };

        if (accountCaller != null)
        {
            collectedPlayerData.IsWatched = accountCaller.SavedProfiles.Contains(playerGuid);
        }

        return collectedPlayerData;
    }

    /// <summary>
    /// Returns the total number of replays in the database.
    /// </summary>
    public async Task<int> GetTotalReplayCount()
    {
        return await _context.Replays.CountAsync();
    }

    public async Task<Replay?> GetReplay(int id, AuthenticationState authstate)
    {
        var replay = await _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundEndPlayers)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (replay == null)
            return null;

        var caller = await _accountService.GetAccount(authstate);
        replay = FilterReplay(replay, caller);
        return replay;
    }

    private Replay FilterReplay(Replay replay, Account? caller = null)
    {
        if (replay.RoundEndPlayers == null)
            return replay;

        var accountDictionary = new Dictionary<Guid, AccountSettings>();
        foreach (var replayRoundEndPlayer in replay.RoundEndPlayers)
        {
            if (!accountDictionary.ContainsKey(replayRoundEndPlayer.PlayerGuid))
            {
                var accountForPlayerTemp = _accountService.GetAccountSettings(replayRoundEndPlayer.PlayerGuid);
                if (accountForPlayerTemp == null)
                {
                    continue;
                }

                accountDictionary.Add(replayRoundEndPlayer.PlayerGuid, accountForPlayerTemp);
            }
            var accountForPlayer = accountDictionary[replayRoundEndPlayer.PlayerGuid];

            if (!accountForPlayer.RedactInformation) continue;

            if (caller == null)
            {
                replayRoundEndPlayer.RedactInformation();
                continue;
            }

            if (replayRoundEndPlayer.PlayerGuid == caller.Guid) continue;

            // If the caller is an admin, we can show the information.
            if (!caller.IsAdmin)
            {
                replayRoundEndPlayer.RedactInformation();
            }
        }

        return replay;
    }

    public async Task<SearchResult> SearchReplays(List<SearchQueryItem> searchItems, int page, AuthenticationState authenticationState)
    {
        var callerAccount = await _accountService.GetAccount(authenticationState);

        foreach (var searchQueryItem in searchItems.Where(x => x.SearchModeEnum == SearchMode.PlayerOocName))
        {
            var query = searchQueryItem.SearchValue;

            var foundOocAccount = _context.Accounts
                .Include(a => a.Settings)
                .FirstOrDefault(a => a.Username.ToLower().Equals(query.ToLower()));

            if (callerAccount != null)
            {
                if (!callerAccount.Username.ToLower().Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundOocAccount != null && foundOocAccount.Settings.RedactInformation)
                    {
                        if (callerAccount == null || !callerAccount.IsAdmin)
                        {
                            if (foundOocAccount.Protected)
                            {
                                throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                            }
                            else
                            {
                                throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                            }
                        }
                    }
                }
            } else if (foundOocAccount != null && foundOocAccount.Settings.RedactInformation)
            {
                if (foundOocAccount.Protected)
                {
                    throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                }
                else
                {
                    throw new UnauthorizedAccessException(
                        "The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                }
            }
        }

        foreach (var searchQueryItem in searchItems.Where(x => x.SearchModeEnum == SearchMode.Guid))
        {
            var query = searchQueryItem.SearchValue;

            var foundGuidAccount = _context.Accounts
                .Include(a => a.Settings)
                .FirstOrDefault(a => a.Guid.ToString().ToLower().Contains(query.ToLower()));

            if (foundGuidAccount != null && foundGuidAccount.Settings.RedactInformation)
            {
                if (callerAccount != null)
                {
                    if (callerAccount.Guid != foundGuidAccount.Guid)
                    {
                        // if the requestor is not the found account and the requestor is not an admin, deny access
                        if (callerAccount == null || !callerAccount.IsAdmin)
                        {
                            if (foundGuidAccount.Protected)
                            {
                                throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                            }
                            else
                            {
                                throw new UnauthorizedAccessException(
                                    "The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                            }
                        }
                    }
                } else
                {
                    if (foundGuidAccount.Protected)
                    {
                        throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                    }
                    else
                    {
                        throw new UnauthorizedAccessException(
                            "The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                    }
                }
            } else if (foundGuidAccount != null && foundGuidAccount.Settings.RedactInformation)
            {
                if (foundGuidAccount.Protected)
                {
                    throw new UnauthorizedAccessException("This account is protected and redacted. This might happens due to harassment or other reasons.");
                }
                else
                {
                    throw new UnauthorizedAccessException(
                        "The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                }
            }
        }

        Task.Run(async () =>
        {
            await _accountService.AddHistory(callerAccount, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.SearchPerformed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = string.Join(", ", searchItems.Select(x => $"{x.SearchMode}={x.SearchValue}"))
            });
        });

        var (replays, results, wasCache) = SearchReplays(searchItems, page, Constants.ReplaysPerPage);

        return new SearchResult()
        {
            Replays = replays,
            PageCount = (int) Math.Ceiling((double) results / Constants.ReplaysPerPage),
            CurrentPage = page,
            TotalReplays = results,
            IsCache = wasCache,
            SearchItems = searchItems
        };
    }

    public async Task<PlayerData?> HasProfile(string username, AuthenticationState state)
    {
        var accountGuid = AccountHelper.GetAccountGuid(state);

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.PlayerOocName.ToLower() == username.ToLower());
        if (player == null)
        {
            return null;
        }

        var account = await _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Username == username);

        if (account != null && account.Settings.RedactInformation && account.Guid != accountGuid)
        {
            var requestor = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Guid == accountGuid);

            if (requestor == null || !requestor.IsAdmin)
            {
                return null;
            }
        }

        return new PlayerData()
        {
            PlayerGuid = player.PlayerGuid,
            Username = player.PlayerOocName
        };
    }

    /// <summary>
    /// Searches a list of replays for a specific query.
    /// </summary>
    /// <returns>
    /// A list of replays that match the search query.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// Thrown when the search mode is not implemented.
    /// </exception>
    private (List<ReplayResult> replays, int results, bool wasCache) SearchReplays(List<SearchQueryItem> searchItems, int page, int pageSize)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var cacheKey = $"{string.Join("-", searchItems.Select(x => $"{x.SearchMode}-{x.SearchValue}"))}";

        var queryable = _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundEndPlayers).AsQueryable();

        foreach (var searchItem in searchItems)
        {
            // Note: npgsql supposedly translates "string1.ToLower().Contains(string2.ToLower())" into an ILIKE
            // There's "EF.Functions.ILike(string1, pattern)" but I'm not sure how injection-resistant it is
            queryable = searchItem.SearchModeEnum switch
            {
                SearchMode.Map => queryable.Where(
                    x => x.Map.ToLower().Contains(searchItem.SearchValue.ToLower())
                    || x.Maps.Any(map => map.ToLower().Contains(searchItem.SearchValue.ToLower()))
                ),
                SearchMode.Gamemode => queryable.Where(x => x.Gamemode.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.ServerId => queryable.Where(x => x.ServerId.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.Guid => queryable.Where(r => r.RoundEndPlayers.Any(p => p.PlayerGuid.ToString().ToLower().Contains(searchItem.SearchValue.ToLower()))),
                SearchMode.PlayerIcName => queryable.Where(r => r.RoundEndPlayers.Any(p => p.PlayerIcName.ToLower().Contains(searchItem.SearchValue.ToLower()))),
                SearchMode.PlayerOocName => queryable.Where(r => r.RoundEndPlayers.Any(p => p.PlayerOocName.ToLower().Contains(searchItem.SearchValue.ToLower()))),
                // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall (its lying, this works)
                SearchMode.RoundEndText => queryable.Where(x => x.RoundEndTextSearchVector.Matches(searchItem.SearchValue)),
                SearchMode.ServerName => queryable.Where(x => x.ServerName != null && x.ServerName.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.RoundId => queryable.Where(x => x.RoundId != null && x.RoundId.ToString().ToLower().Contains(searchItem.SearchValue.ToLower())),
                _ => throw new NotImplementedException(),
            };
        }

        queryable = queryable
            .OrderByDescending(r => r.Date ?? DateTime.MinValue);


        // Technically it might be inaccurate. In practice nobody will care much?
        int totalItems = _cache.GetOrCreate(cacheKey, e =>
        {
            e.SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
            e.SetSlidingExpiration(TimeSpan.FromMinutes(35));
            return queryable.Count();
        });

        // Get all results and store them in the cache
        var allResults = queryable
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(r => r.ToResult())
            .ToList();

        stopWatch.Stop();
        Log.Information("Search took " + stopWatch.ElapsedMilliseconds + "ms.");

        return (allResults, totalItems, false);
    }

    public async Task<List<ReplayResult>?> GetFavorites(AuthenticationState authState)
    {
        var account = await _accountService.GetAccount(authState);
        if (account == null)
        {
            return null;
        }

        var replays = await _context.Replays
            .Include(r => r.RoundEndPlayers)
            .Where(r => account.FavoriteReplays.Contains(r.Id))
            .Select(r => r.ToResult())
            .ToListAsync();

        return replays;
    }
}