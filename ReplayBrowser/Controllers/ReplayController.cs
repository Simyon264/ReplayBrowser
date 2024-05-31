using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
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
    
    public ReplayController(ReplayDbContext dbContext, AccountService accountService)
    {
        _dbContext = dbContext;
        _accountService = accountService;
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