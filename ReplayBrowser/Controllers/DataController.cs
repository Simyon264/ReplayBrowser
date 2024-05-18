using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("api/Data/")]
public class DataController : Controller
{
    private readonly ReplayDbContext _context;
    
    public DataController(ReplayDbContext context)
    {
        _context = context;
    }
    
    [HttpGet("username-completion")]
    public async Task<List<string>> GetUsernameCompletion(
        [FromQuery] string username)
    {
        var completions = await _context.Players
            .Where(p => p.PlayerOocName.ToLower().StartsWith(username.ToLower()))
            .Select(p => p.PlayerOocName)
            .Distinct() // Remove duplicates
            .Take(10)
            .ToListAsync();

        return completions;
    }
}