using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
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
        
        return replays;
    }

    public async Task<CollectedPlayerData?> GetPlayerProfile(Guid playerGuid, AuthenticationState authenticationState)
    {
        var accountCaller = await _accountService.GetAccount(authenticationState);
        
        var accountRequested = _accountService.GetAccountSettings(playerGuid);
        
        if (accountRequested is { RedactInformation: true })
        {
            if (accountCaller == null || accountCaller.Guid != playerGuid)
            {
                switch (accountCaller)
                {
                    case null:
                        throw new UnauthorizedAccessException("This account is private.");
                    case { IsAdmin: false }:
                        throw new UnauthorizedAccessException("This account is private.");
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
        
        await _accountService.AddHistory(accountCaller, new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.ProfileViewed) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = $"Player GUID: {playerGuid} Username: {collectedPlayerData.PlayerData.Username}"
        });
        
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

    public async Task<SearchResult> SearchReplays(SearchMode searchMode, string query, int page, AuthenticationState authenticationState)
    {
        var callerAccount = await _accountService.GetAccount(authenticationState);
        
        switch (searchMode)
        {
            case SearchMode.Guid:
                var foundGuidAccount = _context.Accounts
                    .Include(a => a.Settings)
                    .FirstOrDefault(a => a.Guid.ToString().ToLower().Contains(query.ToLower()));
                
                if (foundGuidAccount != null && foundGuidAccount.Settings.RedactInformation)
                {
                    if (callerAccount != null)
                    {
                        if (callerAccount.Guid == foundGuidAccount.Guid)
                        {
                            break;
                        }
                    }
                    
                    // if the requestor is not the found account and the requestor is not an admin, deny access
                    if (callerAccount == null || !callerAccount.IsAdmin)
                    {
                        throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                    }
                }
                break;
            
            case SearchMode.PlayerOocName:
                var foundOocAccount = _context.Accounts
                    .Include(a => a.Settings)
                    .FirstOrDefault(a => a.Username.ToLower().Contains(query.ToLower()));

                if (callerAccount != null && callerAccount.Username.ToLower().Contains(query.ToLower()))
                {
                    break;
                }
                
                if (foundOocAccount != null && foundOocAccount.Settings.RedactInformation)
                {
                    // if the requestor is not the found account and the requestor is not an admin, deny access
                    if (callerAccount == null || !callerAccount.IsAdmin)
                    {
                        throw new UnauthorizedAccessException("The account you are trying to search for is private. Contact the account owner and ask them to make their account public.");
                    }
                }
                break;
        }

        await _accountService.AddHistory(callerAccount, new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.SearchPerformed) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = $"Mode: {searchMode}, Query: {query}"
        });
        
        var found = SearchReplays(searchMode, query, page, Constants.ReplaysPerPage);
        var pageCount = (int) Math.Ceiling((double) found.results / Constants.ReplaysPerPage);
        var replays = FilterReplays(found.Item1, callerAccount?.Guid);
        
        return new SearchResult()
        {
            Replays = replays,
            PageCount = pageCount,
            CurrentPage = page,
            TotalReplays = found.Item2,
            IsCache = found.Item3,
            SearchMode = searchMode,
            Query = query
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
    private (List<Replay> replays, int results, bool wasCache) SearchReplays(SearchMode mode, string query, int page, int pageSize)
    {
        var cacheKey = $"{mode}-{query}-{pageSize}";
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
        var queryable = _context.Replays.AsQueryable();

        
        IIncludableQueryable<Player, Replay?>? players;
        IQueryable<int?>? replayIds;
        switch (mode)
        {
            case SearchMode.Map:
                queryable = queryable.Where(x => x.Map.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.Gamemode:
                queryable = queryable.Where(x => x.Gamemode.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.ServerId:
                queryable = queryable.Where(x => x.ServerId.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.Guid:
                players = _context.Players
                    .Where(p => p.PlayerGuid.ToString().ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = _context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.PlayerIcName:
                players = _context.Players
                    .Where(p => p.PlayerIcName.ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = _context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.PlayerOocName:
                players = _context.Players
                    .Where(p => p.PlayerOocName.ToLower().Contains(query.ToLower()))
                    .Include(p => p.Replay);
                replayIds = players.Select(p => p.ReplayId).Distinct();
                queryable = _context.Replays.Where(r => replayIds.Contains(r.Id));
                break;
            case SearchMode.RoundEndText:
                // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall (its lying, this works)
                queryable = queryable.Where(x => x.RoundEndTextSearchVector.Matches(query));
                break;
            case SearchMode.ServerName:
                queryable = queryable.Where(x => x.ServerName != null && x.ServerName.ToLower().Contains(query.ToLower()));
                break;
            case SearchMode.RoundId:
                queryable = queryable.Where(x => x.RoundId != null && x.RoundId.ToString().Contains(query));
                break;
            default:
                throw new NotImplementedException();
        }
    
        var totalItems = queryable.Count();

        // Get all results and store them in the cache
        var allResults = queryable
            .Include(r => r.RoundEndPlayers)
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

        _cache.Set(cacheKey, paginatedResults, TimeSpan.FromMinutes(5));

        stopWatch.Stop();
        Log.Information("Search took " + stopWatch.ElapsedMilliseconds + "ms.");

        if (page < paginatedResults.Count)
        {
            return (paginatedResults[page].Item1, paginatedResults[page].Item2, false);
        }

        return (new List<Replay>(), 0, false);
    }
}