using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("/account/")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ReplayDbContext _context;
    
    public AccountController(IConfiguration configuration, ReplayDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }
    
    [Route("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = _configuration["RedirectUri"]
        });
    }
    
    [Route("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/");
    }

    /// <summary>
    /// Deletes the account from the logged in user.
    /// </summary>
    [HttpGet("delete")]
    public async Task<IActionResult> DeleteAccount()
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        var guid = AccountHelper.GetAccountGuid(User);
        
        var user = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guid);
        
        if (user == null)
        {
            return NotFound("Account is null. This should not happen.");
        }
        
        _context.Accounts.Remove(user);
        await _context.SaveChangesAsync();
        
        await HttpContext.SignOutAsync("Cookies");
        // Redirect to the home page
        return Redirect("/");
    }


    /// <summary>
    /// Downloads the data of a specific guid. This returns a zip file with every replay that the player has played as well as a copy of their account and history if they have one.
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [HttpGet("download-data-admin")]
    [Authorize]
    public async Task<IActionResult> DownloadDataAdmin(
        [FromQuery] string guid
        )
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return BadRequest("Guid is null or empty.");
        }
        
        if (!Guid.TryParse(guid, out var parsedGuid))
        {
            return BadRequest("Guid is not a valid guid.");
        }
        
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        var guidRequestor = AccountHelper.GetAccountGuid(User);
        
        var requestor = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guidRequestor);
        
        if (requestor == null)
        {
            return NotFound("Account is null. This should not happen.");
        }
        
        if (!requestor.IsAdmin) 
            return Unauthorized("You are not an admin.");
        
        var user = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == parsedGuid);
        
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            if (user != null)
            {
                var historyEntry = archive.CreateEntry("history.json");
                using (var entryStream = historyEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, user.History);
                }

                user.History = null;

                var baseEntry = archive.CreateEntry("user.json");
                using (var entryStream = baseEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, user, new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
                }
            }

            var replays = await _context.Replays
                .Include(r => r.RoundEndPlayers)
                .Where(r => r.RoundEndPlayers != null && r.RoundEndPlayers.Any(p => p.PlayerGuid == parsedGuid))
                .ToListAsync();

            foreach (var replay in replays)
            {
                var replayEntry = archive.CreateEntry($"replay-{replay.Id}.json");
                using (var entryStream = replayEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, replay, new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles
                    });
                }
            }
        }
        
        zipStream.Seek(0, SeekOrigin.Begin);
        var fileName = $"account-gdpr-{user.Guid}_{DateTime.Now:yyyy-MM-dd}.zip";
        return File(zipStream, "application/zip", fileName);
    }
    
    /// <summary>
    /// Removed a specific guid permanently from the database. Future replays will have this player replaced with "Removed by GDPR request".
    /// </summary>
    /// <returns></returns>
    [HttpPost("delete-admin")]
    [Authorize]
    public async Task<IActionResult> AdminDelete(
        [FromQuery] string guid
        )
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return BadRequest("Guid is null or empty.");
        }
        
        if (!Guid.TryParse(guid, out var parsedGuid))
        {
            return BadRequest("Guid is not a valid guid.");
        }
        
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        var guidRequestor = AccountHelper.GetAccountGuid(User);
        
        var requestor = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guidRequestor);
        
        if (requestor == null)
        {
            return NotFound("Account is null. This should not happen.");
        }
        
        if (!requestor.IsAdmin) 
            return Unauthorized("You are not an admin.");
        
        var user = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == parsedGuid);
        
        if (user != null) 
        {
            _context.Accounts.Remove(user);
        }

        _context.GdprRequests.Add(new GdprRequest
        {
            Guid = parsedGuid
        });

        _context.Replays
            .Include(replay => replay.RoundEndPlayers)
            .Where(r => r.RoundEndPlayers != null && r.RoundEndPlayers.Any(p => p.PlayerGuid == parsedGuid))
            .ToList()
            .ForEach(r =>
            {
                r.RoundEndPlayers!
                    .Where(p => p.PlayerGuid == parsedGuid)
                    .ToList()
                    .ForEach(p => p.RedactInformation(true));
            });
        
        await _context.SaveChangesAsync();
        return Ok();
    }
    
    /// <summary>
    /// Returns a zip of the current users stored data.
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> DownloadAccount()
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        var guid = AccountHelper.GetAccountGuid(User);
        
        var user = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guid);
        
        if (user == null)
        {
            return NotFound("Account is null. This should not happen.");
        }
        
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var historyEntry = archive.CreateEntry("history.json");
            using (var entryStream = historyEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, user.History);
            }
            
            user.History = null;

            var baseEntry = archive.CreateEntry("user.json");
            using (var entryStream = baseEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, user, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }
        
        zipStream.Seek(0, SeekOrigin.Begin);
        
        var fileName = $"account-{user.Guid}_{DateTime.Now:yyyy-MM-dd}.zip";
        return File(zipStream, "application/zip", fileName);
    }
}