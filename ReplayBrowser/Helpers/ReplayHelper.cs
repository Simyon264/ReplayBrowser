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
    const string REDACTION_MESSAGE = "The account you are trying to search for is private or deleted. This might happen for various reasons as chosen by the account owner or the site administrative decision";

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
        // "Execution of the current method continues before the call is completed" is a desired outcome here
        #pragma warning disable CS4014
        Task.Run(async () =>
        {
            await _accountService.AddHistory(account, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.MainPageViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = string.Empty
            });
        });
        #pragma warning restore CS4014

        return replays;
    }

    /// <summary>
    /// Fetches a player profile from the database.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the account is private and the requestor is not the account owner or an admin.</exception>
    public async Task<CollectedPlayerData> GetPlayerProfile(Guid playerGuid, AuthenticationState authenticationState, bool skipPermsCheck = false)
    {
        var accountCaller = await _accountService.GetAccount(authenticationState);

        var isGdpr = await _context.GdprRequests.AnyAsync(g => g.Guid == playerGuid);
        if (isGdpr)
            throw new UnauthorizedAccessException(REDACTION_MESSAGE);

        var accountRequested = _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefault(a => a.Guid == playerGuid);

        if (!skipPermsCheck)
        {
            CheckAccountAccess(caller: accountCaller, found: accountRequested);

            await _accountService.AddHistory(accountCaller, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.ProfileViewed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = $"Player GUID: {playerGuid} Username: {(await _apiHelper.FetchPlayerDataFromGuid(playerGuid)).Username ?? "Unknown"}"
            });
        }

        var replayPlayers = await _context.Players
            .AsNoTracking()
            .Where(p => p.Participant.PlayerGuid == playerGuid)
            .Where(p => p.Participant.Replay!.Date != null)
            .Select(p => new {
                p.Id,
                p.Participant.ReplayId,
                Date = (DateTime) p.Participant.Replay!.Date!,
                p.Participant.Replay!.Duration,
                p.JobPrototypes,
                // .Antag becomes unnecessary because this always has at least an empty string,
                p.AntagPrototypes,
                p.PlayerIcName,
            })
            .ToListAsync();

        if (replayPlayers.Count == 0)
        {
            return new CollectedPlayerData()
            {
                PlayerData = new PlayerData()
                {
                    PlayerGuid = playerGuid,
                    Username = (await _apiHelper.FetchPlayerDataFromGuid(playerGuid)).Username ??
                               "Unable to fetch username (API error)"
                },
                PlayerGuid = playerGuid,
                Characters = new List<CharacterData>(),
                TotalEstimatedPlaytime = TimeSpan.Zero,
                TotalRoundsPlayed = 0,
                TotalAntagRoundsPlayed = 0,
                LastSeen = DateTime.MinValue,
                JobCount = new List<JobCountData>(),
                GeneratedAt = DateTime.UtcNow
            };
        }

        var replayPlayerGroup = replayPlayers.GroupBy(rp => rp.ReplayId);

        var totalRoundsPlayed = replayPlayerGroup.Count();
        var totalAntagRoundsPlayed = replayPlayerGroup.Count(rpg => rpg.Any(rp => rp.AntagPrototypes.Count > 0));

        // Estimated
        var totalPlaytime = new TimeSpan(replayPlayerGroup.Sum(rpg => (TimeSpan.TryParse(rpg.First().Duration, out var durationSpan) ? durationSpan : TimeSpan.Zero).Ticks));

        var lastSeen = replayPlayerGroup.Max(g => g.First().Date);

        var charactersPlayed = replayPlayers.GroupBy(rp => rp.PlayerIcName).Select(rpg => new CharacterData {
            CharacterName = rpg.Key,
            LastPlayed = rpg.Max(rp => rp.Date),
            RoundsPlayed = rpg.Count()
        }).ToList();

        var jobCount = replayPlayers.Where(rp => rp.JobPrototypes.Count > 0)
            .GroupBy(rp => rp.JobPrototypes[0])
            .Select(rpg => new JobCountData {
                JobPrototype = rpg.Key,
                RoundsPlayed = rpg.Count(),
                LastPlayed = rpg.Max(rp => rp.Date)
            }).ToList();

        CollectedPlayerData collectedPlayerData = new()
        {
            PlayerData = new PlayerData()
            {
                PlayerGuid = playerGuid,
                Username = (await _apiHelper.FetchPlayerDataFromGuid(playerGuid)).Username ??
                           "Unable to fetch username (API error)"
            },
            PlayerGuid = playerGuid,
            Characters = charactersPlayed,
            TotalEstimatedPlaytime = totalPlaytime,
            TotalRoundsPlayed = totalRoundsPlayed,
            TotalAntagRoundsPlayed = totalAntagRoundsPlayed,
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

    public async Task<Replay?> GetReplay(string @operator, string server, int id, AuthenticationState authstate)
    {
        var replay = await _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundParticipants!)
            .ThenInclude(p => p.Players)
            .OrderByDescending(r => r.Duration)
            .FirstOrDefaultAsync(r => r.ServerId == @operator && r.ServerName == server && r.RoundId == id);

        if (replay == null)
            return null;

        var caller = await _accountService.GetAccount(authstate);
        replay = FilterReplay(replay, caller);
        return replay;
    }

    public async Task<Replay?> GetReplay(int id, AuthenticationState authstate)
    {
        var replay = await _context.Replays
            .AsNoTracking()
            .Include(r => r.RoundParticipants!)
            .ThenInclude(p => p.Players)
            .SingleOrDefaultAsync(r => r.Id == id);

        if (replay == null)
            return null;

        var caller = await _accountService.GetAccount(authstate);
        replay = FilterReplay(replay, caller);
        return replay;
    }

    private Replay FilterReplay(Replay replay, Account? caller = null)
    {
        if (replay.RoundParticipants == null)
            return replay;

        if (caller is not null && caller.IsAdmin)
            return replay;

        var redactFor = replay.RoundParticipants!
            .Where(p => p.PlayerGuid != Guid.Empty)
            .Select(p => new { p.PlayerGuid, Redact = _accountService.GetAccountSettings(p.PlayerGuid)?.RedactInformation ?? false })
            .Where(p => p.Redact)
            .Select(p => p.PlayerGuid)
            .ToList();

        if (caller is not null)
            redactFor.Remove(caller.Guid);

        foreach (var redact in redactFor)
            replay.RedactInformation(redact, false);

        replay.RedactCleanup();

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

            CheckAccountAccess(caller: callerAccount, found: foundOocAccount);
        }

        foreach (var searchQueryItem in searchItems.Where(x => x.SearchModeEnum == SearchMode.Guid))
        {
            var query = searchQueryItem.SearchValue;

            var foundGuidAccount = _context.Accounts
                .Include(a => a.Settings)
                // This .ToLower & .Contains trick allows for partially matching against a GUID
                .FirstOrDefault(a => a.Guid.ToString().ToLower().Contains(query.ToLower()));

            CheckAccountAccess(caller: callerAccount, found: foundGuidAccount);
        }

        // "Execution of the current method continues before the call is completed" is a desired outcome here
        #pragma warning disable CS4014
        Task.Run(async () =>
        {
            await _accountService.AddHistory(callerAccount, new HistoryEntry()
            {
                Action = Enum.GetName(typeof(Action), Action.SearchPerformed) ?? "Unknown",
                Time = DateTime.UtcNow,
                Details = string.Join(", ", searchItems.Select(x => $"{x.SearchModeEnum}={x.SearchValue}"))
            });
        });
        #pragma warning restore CS4014

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

    /// <summary>
    /// Check whether the caller account (first arg) has access to view the found account (second arg)
    /// </summary>
    /// <remarks>
    /// I am really not a fan of two params of same type being used here. It can and probably will lead to confusing them around.
    /// TODO: Investigate what's the diff between <see cref="AccountSettings.RedactInformation"/> and <see cref="Account.Protected"/>
    /// </remarks>
    public static void CheckAccountAccess(Account? caller, Account? found)
    {
        // There's no account to worry about yay
        if (found is null)
            return;

        // Is there any redaction to worry about?
        if (!found.Settings.RedactInformation)
            return;
        // Ah shit

        // Not the person we're looking for
        if (caller is null)
            throw new UnauthorizedAccessException(REDACTION_MESSAGE);

        // Admins can see everything. Without this we could just peek into the DB.
        if (caller.IsAdmin)
            return;

        // Same person (or at least account), let them at it
        if (caller.Guid == found.Guid)
            return;

        // Catch-all
        // Don't give more info about why, what, just use a generic message for everything
        // For debugging you can always just check the logs or DB
        // Giving specific info like "admin" vs "self redacted" vs "GDPR request"
        throw new UnauthorizedAccessException(REDACTION_MESSAGE);
    }

    public async Task<PlayerData?> HasProfile(string username, AuthenticationState state)
    {
        var accountGuid = AccountHelper.GetAccountGuid(state);

        var player = await _context.ReplayParticipants
            .FirstOrDefaultAsync(p => p.Username.ToLower() == username.ToLower());
        if (player == null)
        {
            return null;
        }

        var account = await _context.Accounts
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Guid == player.PlayerGuid);

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
            Username = player.Username
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

        var cacheKey = $"{string.Join("-", searchItems.Select(x => $"{x.SearchModeEnum}-{x.SearchValue}"))}";

        var queryable = _context.Replays
            .AsNoTracking()
            .AsQueryable();

        foreach (var searchItem in searchItems)
        {
            // Note: npgsql supposedly translates "string1.ToLower().Contains(string2.ToLower())" into an ILIKE
            // There's "EF.Functions.ILike(string1, pattern)" but I'm not sure how injection-resistant it is
            queryable = searchItem.SearchModeEnum switch
            {
                SearchMode.Map => queryable.Where(
                    x => x.Map!.ToLower().Contains(searchItem.SearchValue.ToLower())
                    || x.Maps!.Any(map => map.ToLower().Contains(searchItem.SearchValue.ToLower()))
                ),
                SearchMode.Gamemode => queryable.Where(x => x.Gamemode.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.ServerId => queryable.Where(x => x.ServerId.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.Guid => queryable.Where(r => r.RoundParticipants!.Any(p => p.PlayerGuid.ToString().ToLower().Contains(searchItem.SearchValue.ToLower()))),
                SearchMode.PlayerIcName => queryable.Where(r => r.RoundParticipants!.Any(p => p.Players!.Any(pl => pl.PlayerIcName.ToLower().Contains(searchItem.SearchValue.ToLower())))),
                SearchMode.PlayerOocName => queryable.Where(r => r.RoundParticipants!.Any(p => p.Username.ToLower().Contains(searchItem.SearchValue.ToLower()))),
                // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall (its lying, this works)
                SearchMode.RoundEndText => queryable.Where(x => x.RoundEndTextSearchVector.Matches(searchItem.SearchValue)),
                SearchMode.ServerName => queryable.Where(x => x.ServerName != null && x.ServerName.ToLower().Contains(searchItem.SearchValue.ToLower())),
                SearchMode.RoundId => queryable.Where(x => x.RoundId != null && x.RoundId!.ToString()!.ToLower().Contains(searchItem.SearchValue.ToLower())),
                _ => throw new NotImplementedException(),
            };
        }

        // Get total result count, cache it
        // Technically it might be inaccurate. In practice nobody will care much?
        int totalItems = _cache.GetOrCreate(cacheKey, e =>
        {
            e.SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
            e.SetSlidingExpiration(TimeSpan.FromMinutes(35));
            return queryable.Count();
        });

        var allResults = queryable
            .OrderByDescending(r => r.Date ?? DateTime.MinValue)
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
            .Where(r => account.FavoriteReplays.Contains(r.Id))
            .Select(r => r.ToResult())
            .ToListAsync();

        return replays;
    }
}
