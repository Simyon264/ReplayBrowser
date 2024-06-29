using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Services;
using ReplayBrowser.Services.ReplayParser;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("api/Data/")]
public class DataController : Controller
{
    private readonly ReplayDbContext _context;
    private readonly ProfilePregeneratorService _profilePregeneratorService;
    
    public DataController(ReplayDbContext context, ProfilePregeneratorService profilePregeneratorService)
    {
        _context = context;
        _profilePregeneratorService = profilePregeneratorService;
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
    
    /// <summary>
    /// Reports the download progress of all replays.
    /// </summary>
    /// <returns></returns>
    [HttpGet("download-progress")]
    public DownloadProgress GetDownloadProgress()
    {
        return new DownloadProgress()
        {
            Progress = ReplayParserService.DownloadProgress.ToDictionary(x => x.Key, x => x.Value),
            Status = ReplayParserService.Status.ToFriendlyString(),
            Details = ReplayParserService.Details,
            PregenerationProgress = _profilePregeneratorService.PregenerationProgress
        };
    }
}

public class DownloadProgress
{
    public required string Status { get; set; }
    public required Dictionary<string, double> Progress { get; set; }
    
    public required string Details { get; set; }
    public required ProfilePregeneratorService.PreGenerationProgress PregenerationProgress { get; set; }
}