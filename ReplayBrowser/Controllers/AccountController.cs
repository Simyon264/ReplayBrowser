using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
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