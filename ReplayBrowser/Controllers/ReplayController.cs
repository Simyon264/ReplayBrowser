using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;
using ReplayBrowser.Services;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("api/Replay/")]
[Authorize]
public class ReplayController : Controller
{
    private readonly ReplayDbContext _dbContext;
    private readonly AccountService _accountService;
    private readonly ReplayHelper _replayHelper;

    public ReplayController(ReplayDbContext dbContext, AccountService accountService, ReplayHelper replayHelper)
    {
        _dbContext = dbContext;
        _accountService = accountService;
        _replayHelper = replayHelper;
    }

    [HttpGet("{replayId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReplay(int replayId)
    {
        var authState = new AuthenticationState(HttpContext.User);
        var replay = await _replayHelper.GetReplay(replayId, authState);
        if (replay == null)
        {
            return NotFound();
        }

        return Ok(replay);
    }

    /// <summary>
    /// Deletes all stored profiles for a server group.
    /// </summary>

    [HttpDelete("profile/delete/{serverId}")]
    public async Task<IActionResult> DeleteProfile(string serverId)
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guidRequestor = AccountHelper.GetAccountGuid(User);

        var requestor = await _dbContext.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guidRequestor);

        if (requestor == null)
        {
            return NotFound("Account is null. This should not happen.");
        }

        if (!requestor.IsAdmin)
            return Unauthorized("You are not an admin.");

        var players = await _dbContext.Replays
            .Where(r => r.ServerId == serverId)
            .Include(r => r.RoundParticipants)
            .Where(r => r.RoundParticipants != null)
            .SelectMany(r => r.RoundParticipants!)
            .Select(p => p.PlayerGuid)
            .Distinct()
            .ToListAsync();

        foreach (var player in players)
        {
            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "CharacterData"
                WHERE "CollectedPlayerDataPlayerGuid" = {player};
                """
            );

            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "JobCountData"
                WHERE "CollectedPlayerDataPlayerGuid" = {player};
                """
            );
        }

        await _dbContext.PlayerProfiles
            .Where(p => players.Contains(p.PlayerGuid))
            .ExecuteDeleteAsync();

        return Ok();
    }

    [HttpGet("profile/{profileGuid:guid}")]
    public async Task<IActionResult> GetPlayerData(Guid profileGuid)
    {
        // ok very jank, we construct a AuthenticationState object from the current user
        var authState = new AuthenticationState(HttpContext.User);

        try
        {
            return Ok(await _replayHelper.GetPlayerProfile(profileGuid, authState));
        }
        catch (UnauthorizedAccessException e)
        {
            return Unauthorized(e.Message);
        }
    }

    /// <summary>
    /// Marks a profile "watched" for the current user.
    /// </summary>
    /// <returns>True if the profile is now watched, false if it is now unwatched.</returns>
    [HttpPost("watch/{profileGuid:guid}")]
    public async Task<IActionResult> WatchProfile(Guid profileGuid)
    {
        var guid = AccountHelper.GetAccountGuid(HttpContext.User);
        var account = await _dbContext.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guid);

        if (account == null)
        {
            return Unauthorized();
        }

        var isWatched = account.SavedProfiles.Contains(profileGuid);

        if (!account.SavedProfiles.Remove(profileGuid))
        {
            account.SavedProfiles.Add(profileGuid);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(!isWatched);
    }


    /// <summary>
    /// Marks a replay as a favorite for the current user.
    /// </summary>
    /// <returns>True if the replay is now favorited, false if it is now unfavorited.</returns>
    [HttpPost("favourite/{replayId}")]
    public async Task<IActionResult> FavoriteReplay(int replayId)
    {
        var guid = AccountHelper.GetAccountGuid(HttpContext.User);
        var account = await _dbContext.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guid);

        if (account == null)
        {
            return Unauthorized();
        }

        var replay = await _dbContext.Replays.FindAsync(replayId);
        if (replay == null)
        {
            return NotFound();
        }

        var isFavorited = account.FavoriteReplays.Contains(replayId);

        if (!account.FavoriteReplays.Remove(replayId))
        {
            account.FavoriteReplays.Add(replayId);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(!isFavorited);
    }
}