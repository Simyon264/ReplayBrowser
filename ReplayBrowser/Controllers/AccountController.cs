using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Helpers;
using ReplayBrowser.Services;
using Serilog;
using Action = ReplayBrowser.Data.Models.Account.Action;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("/account/")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ReplayDbContext _context;
    private readonly Ss14ApiHelper _ss14ApiHelper;
    private readonly AccountService _accountService;

    public AccountController(IConfiguration configuration, ReplayDbContext context, Ss14ApiHelper ss14ApiHelper, AccountService accountService)
    {
        _configuration = configuration;
        _context = context;
        _ss14ApiHelper = ss14ApiHelper;
        _accountService = accountService;
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
    /// Creates an account in the database, then redirects to the home page.
    /// </summary>
    [Authorize]
    [Route("redirect")]
    public async Task<IActionResult> RedirectFromLogin()
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guid = AccountHelper.GetAccountGuid(User);

        if (guid == null)
            return BadRequest("Guid is null. This should not happen.");

        var gdprRequest = await _context.GdprRequests.FirstOrDefaultAsync(g => g.Guid == guid);
        if (gdprRequest != null)
        {
            await HttpContext.SignOutAsync("Cookies");
            return BadRequest("You have requested to be deleted from the database. You cannot create an account.");
        }

        var user = _context.Accounts.FirstOrDefault(a => a.Guid == guid);
        var data = await _ss14ApiHelper.FetchPlayerDataFromGuid((Guid)guid);
        if (user == null)
        {
            user = new Account()
            {
                Guid = (Guid) guid,
                Username = data?.Username ?? "API Error",
            };
            _context.Accounts.Add(user);
            await _context.SaveChangesAsync();
            Log.Information("Created new account for {Guid} with username {Username}", guid, data?.Username);
        }
        else
        {
            if (data?.Username != user.Username)
            {
                user.Username = data?.Username ?? "API Error";
                await _context.SaveChangesAsync();
                Log.Information("Updated username for {Guid} to {Username}", guid, data?.Username);
            }
        }

        // Add login to history
        await _accountService.AddHistory(user, new HistoryEntry()
        {
            Action = Enum.GetName(typeof(Action), Action.Login) ?? "Unknown",
            Time = DateTime.UtcNow,
            Details = null
        });

        return Redirect("/");
    }

    /// <summary>
    /// Deletes the account from the logged in user.
    /// </summary>
    [HttpGet("delete")]
    public async Task<IActionResult> DeleteAccount(
        [FromQuery] bool permanently = false
        )
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guid = AccountHelper.GetAccountGuid(User)!;

        var user = await _context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Guid == guid);

        if (user == null)
        {
            return NotFound("Account is null. This should not happen.");
        }

        if (permanently)
        {
            _context.GdprRequests.Add(new GdprRequest
            {
                Guid = (Guid) guid
            });

            await _context.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "CharacterData"
                WHERE "CollectedPlayerDataPlayerGuid" = {guid};
                """
            );

            await _context.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM "JobCountData"
                WHERE "CollectedPlayerDataPlayerGuid" = {guid};
                """
            );

            _context.Replays
                .Include(r => r.RoundParticipants!)
                .ThenInclude(r => r.Players)
                .Where(r => r.RoundParticipants != null && r.RoundParticipants.Any(p => p.PlayerGuid == guid))
                .ToList()
                .ForEach(r =>
                {
                    r.RedactInformation(guid, true);
                    // FIXME: Might not work
                    r.RedactCleanup();
                });
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

        if (!User.Identity!.IsAuthenticated)
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
            .AsNoTracking()
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
                    await JsonSerializer.SerializeAsync(entryStream, user.History, new JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles,
                        WriteIndented = true
                    });
                }

                user.History = [];

                var baseEntry = archive.CreateEntry("user.json");
                using (var entryStream = baseEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, user, new JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles,
                        WriteIndented = true
                    });
                }
            }

            var replays = await _context.Replays
                .Include(r => r.RoundParticipants!)
                .ThenInclude(r => r.Players)
                .Where(r => r.RoundParticipants != null && r.RoundParticipants.Any(p => p.PlayerGuid == parsedGuid))
                .ToListAsync();

            foreach (var replay in replays)
            {
                var replayEntry = archive.CreateEntry($"replay-{replay.Id}.json");
                using (var entryStream = replayEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, replay, new JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles,
                        WriteIndented = true
                    });
                }
            }
        }

        zipStream.Seek(0, SeekOrigin.Begin);
        var fileName = $"account-gdpr-{guid}_{DateTime.Now:yyyy-MM-dd}.zip";
        return File(zipStream, "application/zip", fileName);
    }

    /// <summary>
    /// Deletes an account in a way a user also has the option to delete. This does not remove them from future and past replays, instead only the entry in the account table.
    /// </summary>
    [HttpPost("delete-admin-non-gdpr")]
    [Authorize]
    public async Task<IActionResult> AdminDeleteNonGdpr(
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

        if (!User.Identity!.IsAuthenticated)
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
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>
    /// Removed a specific guid permanently from the database. Future replays will have this player replaced with "Removed by GDPR request".
    /// </summary>
    /// <returns></returns>
    [HttpPost("delete-admin")]
    [Authorize]
    public async Task<IActionResult> AdminDeleteGdpr(
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

        if (!User.Identity!.IsAuthenticated)
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
            .Include(replay => replay.RoundParticipants!)
            .ThenInclude(replay => replay.Players)
            .Where(r => r.RoundParticipants != null && r.RoundParticipants.Any(p => p.PlayerGuid == parsedGuid))
            .ToList()
            .ForEach(r =>
            {
                r.RedactInformation(parsedGuid, true);
                // FIXME: Might not work
                r.RedactCleanup();
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
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var guid = AccountHelper.GetAccountGuid(User);

        var user = await _context.Accounts
            .AsNoTracking()
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
                await JsonSerializer.SerializeAsync(entryStream, user.History, new JsonSerializerOptions()
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    WriteIndented = true
                });
            }

            user.History = [];

            var baseEntry = archive.CreateEntry("user.json");
            using (var entryStream = baseEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, user, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    WriteIndented = true
                });
            }
        }

        zipStream.Seek(0, SeekOrigin.Begin);

        var fileName = $"account-{user.Guid}_{DateTime.Now:yyyy-MM-dd}.zip";
        return File(zipStream, "application/zip", fileName);
    }

    [HttpPost("add-protected-account")]
    [Authorize]
    public async Task<IActionResult> AddProtectedAccount(
        [FromQuery] string username
    )
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is null or empty.");
        }

        var playerData = await _ss14ApiHelper.FetchPlayerDataFromUsername(username);

        if (playerData == null)
        {
            return NotFound("Player data is null. This should not happen.");
        }

        if (playerData.PlayerGuid == null || playerData.PlayerGuid == Guid.Empty)
        {
            return NotFound("Player guid is null or empty. This should not happen.");
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
            .FirstOrDefaultAsync(a => a.Guid == playerData.PlayerGuid);

        if (user != null)
        {
            return BadRequest("Account already exists.");
        }

        user = new Account()
        {
            Guid = (Guid) playerData.PlayerGuid,
            Username = playerData.Username,
            Settings = new AccountSettings()
            {
                RedactInformation = true
            },
            Protected = true
        };

        _context.Accounts.Add(user);
        await _context.SaveChangesAsync();
        return Ok();
    }
}