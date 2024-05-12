using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Server.Helpers;
using Shared;
using Shared.Models.Account;
using Action = Shared.Models.Account.Action;

namespace Server.Api;

/// <summary>
/// Handles account-related requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TokenBased")]
public class AccountController : Controller
{
    private readonly ReplayDbContext _context;
    private readonly Ss14ApiHelper _ss14ApiHelper;

    public AccountController(ReplayDbContext context, Ss14ApiHelper ss14ApiHelper)
    {
        _context = context;
        _ss14ApiHelper = ss14ApiHelper;
    }

    [HttpPut("ensure-account-exists")]
    public async Task<IActionResult> EnsureAccountExists(
        [FromHeader] Guid accountGuid
    )
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Guid == accountGuid);
        var data = await _ss14ApiHelper.FetchPlayerDataFromGuid(accountGuid);

        if (account == null)
        {
            account = new Account()
            {
                Guid = accountGuid,
                Username = data.Username
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            
            Log.Information("Created new account for {Guid} with username {Username}", accountGuid, data.Username);
        }
        
        if (account.Username != data.Username)
        {
            account.Username = data.Username;
            await _context.SaveChangesAsync();
            Log.Information("Updated username for account {Guid} to {Username}", accountGuid, data.Username);
        }

        return Ok();
    }
    
    [HttpGet("get-account")]
    public async Task<IActionResult> GetAccount(
        [FromHeader] Guid accountGuid
    )
    {
        var account = await _context.Accounts.Where(a => a.Guid == accountGuid)
            .Include(a => a.Settings).FirstOrDefaultAsync();
        
        if (account == null)
        {
            return NotFound();
        }

        return Ok(account);
    }
    
    [HttpPost("set-account-settings")]
    public async Task<IActionResult> SetAccountSettings(
        [FromHeader] Guid accountGuid
    )
    {
        var settingsStrings = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();
        var settings = JsonSerializer.Deserialize<AccountSettings?>(settingsStrings);
        if (settings == null)
        {
            return BadRequest();
        }
        
        var account = await _context.Accounts
            .Include(a => a.History)
            .Include(a => a.Settings)
            .FirstOrDefaultAsync(a => a.Guid == accountGuid);
        if (account == null)
        {
            return NotFound();
        }

        var historyEntry = new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.AccountSettingsChanged) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = "Old settings:\n" + JsonSerializer.Serialize(account.Settings) + "\n\nNew settings:\n" + JsonSerializer.Serialize(settings)
        };
        account.History.Add(historyEntry);
        
        account.Settings = settings;
        
        await _context.SaveChangesAsync();
        return Ok();
    }
    
    /// <summary>
    /// Promotes an account to an admin account. This only works from localhost.
    /// </summary>
    /// <returns></returns>
    [HttpPatch("promote-account")]
    public async Task<IActionResult> PromoteAccount(
        [FromHeader] Guid accountGuid
    )
    {
        if (!IsLocal(HttpContext.Connection))
        {
            return Unauthorized();
        }
        
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Guid == accountGuid);
        if (account == null)
        {
            return NotFound();
        }

        account.IsAdmin = true;
        await _context.SaveChangesAsync();
        return Ok();
    }
    
    [HttpGet("get-account-history")]
    public async Task<IActionResult> GetAccountHistory(
        [FromHeader] Guid accountGuid,
        [FromQuery] int page,
        [FromQuery] string name
    )
    {
        var requestAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Guid == accountGuid);
        if (requestAccount == null)
        {
            return Unauthorized();
        }
        
        if (!requestAccount.IsAdmin)
        {
            return Unauthorized();
        }
        
        var query = _context.Accounts.Include(a => a.History).AsQueryable();
        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(a => a.Username.Contains(name));
        }
        
        var results = await query.FirstOrDefaultAsync(a => a.Username == name);
        
        if (results == null)
        {
            return NotFound();
        }
        
        results.History = results.History.OrderByDescending(h => h.Time).ToList();
        
        var history = results.History
            .Skip(page * 10)
            .Take(10)
            .ToList();
        
        return Ok(new AccountHistoryResponse()
        {
            History = history, 
            Page = page, 
            TotalPages = (int)Math.Ceiling((double)(results.History.Count / 10))
        });
    }
    
    private bool IsLocal(ConnectionInfo connection)
    {
        var remoteAddress = connection.RemoteIpAddress.ToString();

        // if unknown, assume not local
        if (String.IsNullOrEmpty(remoteAddress))
            return false;

        // check if localhost
        if (remoteAddress == "127.0.0.1" || remoteAddress == "::1")
            return true;

        // compare with local address
        if (remoteAddress == connection.LocalIpAddress.ToString())
            return true;

        return false;
    }
}