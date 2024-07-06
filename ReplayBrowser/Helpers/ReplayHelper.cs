using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;
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
    
    public async Task<List<Replay>> GetMostRecentReplays(AuthenticationState state)
    {
        var replays = await _context.Replays
            .AsNoTracking()
            .OrderByDescending(r => r.Date)
            .Include(r => r.RoundEndPlayers)
            .Take(32)
            .ToListAsync();
        
        var caller = AccountHelper.GetAccountGuid(state);
        replays = FilterReplays(replays, caller);
        var account = await _accountService.GetAccount(state);
        
        await _accountService.AddHistory(account, new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.MainPageViewed) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = string.Empty
        });
        
        for (var i = 0; i < replays.Count; i++)
        {
            var replay = replays[i];
            PopulateExtendedFields(ref replay);
            replays[i] = replay;
        }
        
        return replays;
    }

    /// <summary>
    /// Fetches a player profile from the database.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the account is private and the requestor is not the account owner or an admin.</exception>
    public async Task<CollectedPlayerData?> GetPlayerProfile(Guid playerGuid, AuthenticationState authenticationState, bool skipPermsCheck = false)
    {
        var accountCaller = await _accountService.GetAccount(authenticationState);
        
        var accountRequested = _accountService.GetAccountSettings(playerGuid);

        if (!skipPermsCheck)
        {
            if (accountRequested is { RedactInformation: true })
            {
                if (accountCaller == null || !accountCaller.IsAdmin)
                {
                    if (accountCaller?.Guid != playerGuid)
                    {
                        throw new UnauthorizedAccessException("The account you are trying to view is private. Contact the account owner and ask them to make their account public.");
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
        if (_context.PlayerProfiles.Any(p => p.PlayerGuid == playerGuid))
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
    /// Filters replays based on the account GUID.
    /// Replays for accounts that are private and not the requestor will be filtered out.
    /// </summary>
    private List<Replay> FilterReplays(List<Replay> replays, Guid? caller)
    {
        var callerAccount = _context.Accounts
            .FirstOrDefault(a => a.Guid == caller);
        
        for (var i = 0; i < replays.Count; i++)
        {
            replays[i] = FilterReplay(replays[i], callerAccount);
        }
        
        return replays;
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
        PopulateExtendedFields(ref replay);
        return replay;
    }
    
    private Replay FilterReplay(Replay replay, Account? caller = null)
    {
        if (replay.RoundEndPlayers == null)
            return replay;
        
        foreach (var replayRoundEndPlayer in replay.RoundEndPlayers)
        {
            var accountForPlayer = _accountService.GetAccountSettings(replayRoundEndPlayer.PlayerGuid);
            if (accountForPlayer == null)
            {
                continue;
            }

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
                            throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                        }
                    }
                }
            } else if (foundOocAccount != null && foundOocAccount.Settings.RedactInformation)
            {
                throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
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
                            throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                        }
                    }
                } else
                {
                    throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                }
            } else if (foundGuidAccount != null && foundGuidAccount.Settings.RedactInformation)
            {
                throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
            }
        }

        await _accountService.AddHistory(callerAccount, new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.SearchPerformed) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = string.Join(", ", searchItems.Select(x => $"{x.SearchMode}={x.SearchValue}"))
        });
        
        var found = SearchReplays(searchItems, page, Constants.ReplaysPerPage);
        var pageCount = (int) Math.Ceiling((double) found.results / Constants.ReplaysPerPage);
        var replays = FilterReplays(found.Item1, callerAccount?.Guid);
        
        for (var i = 0; i < replays.Count; i++)
        {
            var replay = replays[i];
            PopulateExtendedFields(ref replay);
            replays[i] = replay;
        }
        
        return new SearchResult()
        {
            Replays = replays,
            PageCount = pageCount,
            CurrentPage = page,
            TotalReplays = found.Item2,
            IsCache = found.Item3,
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
    /// <param name="mode">The search mode.</param>
    /// <param name="query">The search query.</param>
    /// <param name="replays">The list of replays to search.</param>
    /// <returns>
    /// A list of replays that match the search query.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// Thrown when the search mode is not implemented.
    /// </exception>
    private (List<Replay> replays, int results, bool wasCache) SearchReplays(List<SearchQueryItem> searchItems, int page, int pageSize)
    {
        var cacheKey = $"{string.Join("-", searchItems.Select(x => $"{x.SearchMode}-{x.SearchValue}"))}-{pageSize}";
        if (_cache.TryGetValue(cacheKey, out List<(List<Replay>, int)> cachedResult))
        {
            if (page < cachedResult.Count)
            {
                var result = cachedResult[page];
                return (result.Item1, result.Item2, true);
            }
        }

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var queryable = _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundEndPlayers).AsQueryable();

        foreach (var searchItem in searchItems)
        {
            switch (searchItem.SearchModeEnum)
            {
                case SearchMode.Map:
                    queryable = queryable.Where(x => x.Map.ToLower().Contains(searchItem.SearchValue.ToLower()));
                    break;
                case SearchMode.Gamemode:
                    queryable = queryable.Where(x => x.Gamemode.ToLower().Contains(searchItem.SearchValue.ToLower()));
                    break;
                case SearchMode.ServerId:
                    queryable = queryable.Where(x => x.ServerId.ToLower().Contains(searchItem.SearchValue.ToLower()));
                    break;
                case SearchMode.Guid:
                    queryable = queryable.Where(r => r.RoundEndPlayers.Where(p => p.PlayerGuid.ToString().ToLower().Contains(searchItem.SearchValue.ToLower())).Count() > 0);
                    break;
                case SearchMode.PlayerIcName:
                    queryable = queryable.Where(r => r.RoundEndPlayers.Where(p => p.PlayerIcName.ToLower().Contains(searchItem.SearchValue.ToLower())).Count() > 0);
                    break;
                case SearchMode.PlayerOocName:
                    queryable = queryable.Where(r => r.RoundEndPlayers.Where(p => p.PlayerOocName.ToLower().Contains(searchItem.SearchValue.ToLower())).Count() > 0);
                    break;
                case SearchMode.RoundEndText:
                    // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall (its lying, this works)
                    queryable = queryable.Where(x => x.RoundEndTextSearchVector.Matches(searchItem.SearchValue));
                    break;
                case SearchMode.ServerName:
                    queryable = queryable.Where(x => x.ServerName != null && x.ServerName.ToLower().Contains(searchItem.SearchValue.ToLower()));
                    break;
                case SearchMode.RoundId:
                    queryable = queryable.Where(x => x.RoundId != null && x.RoundId.ToString().Contains(searchItem.SearchValue));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    
        var totalItems = queryable.Count();

        // Get all results and store them in the cache
        var allResults = queryable
            .OrderByDescending(r => r.Date ?? DateTime.MinValue)
            .Take(Constants.SearchLimit)
            .ToList();

        var paginatedResults = new List<(List<Replay>, int)>();
        for (int i = 0; i * pageSize < allResults.Count; i++)
        {
            var paginatedList = allResults
                .Skip(i * pageSize)
                .Take(pageSize)
                .ToList();

            paginatedResults.Add((paginatedList, totalItems));
        }

        _cache.Set(cacheKey, paginatedResults, TimeSpan.FromMinutes(35));

        stopWatch.Stop();
        Log.Information("Search took " + stopWatch.ElapsedMilliseconds + "ms.");

        if (page < paginatedResults.Count)
        {
            return (paginatedResults[page].Item1, paginatedResults[page].Item2, false);
        }

        return (new List<Replay>(), 0, false);
    }

    public async Task<List<Replay>?> GetFavorites(AuthenticationState authState)
    {
        var account = await _accountService.GetAccount(authState);
        if (account == null)
        {
            return null;
        }

        var replays = await _context.Replays
            .Include(r => r.RoundEndPlayers)
            .Where(r => account.FavoriteReplays.Contains(r.Id))
            .ToListAsync();

        return FilterReplays(replays, account.Guid);
    }

    private void PopulateExtendedFields(ref Replay replay)
    {
        // Nothing yet.
    }
}