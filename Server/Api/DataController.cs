using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Contains endpoints for data retrieval. Such as search completions, leaderboards, and more.
/// </summary>
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly ReplayDbContext _context;

    public DataController(ReplayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Provides a list of usernames which start with the given username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("username-completion")]
    public async Task<ActionResult> GetUsernameCompletion(
        [FromQuery] string username
    )
    {
        var completions = await _context.Players
            .Where(p => p.PlayerOocName.ToLower().StartsWith(username.ToLower()))
            .Select(p => p.PlayerOocName)
            .Distinct() // Remove duplicates
            .Take(10)
            .ToListAsync();

        return Ok(completions);
    }
}