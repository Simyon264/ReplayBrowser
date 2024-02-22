using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using System.Collections.Generic;

namespace Server.Api;

[ApiController]
[Route("api/[controller]")]
public class ReplayController : ControllerBase
{
    private readonly ReplayDbContext _context;

    public ReplayController(ReplayDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult> UploadReplay(IFormFile file)
    {
        if (!HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Due to security reasons, you can only upload replays from localhost. If you want to add a replay source, please contact the server administrator.");
        }
        
        var stream = file.OpenReadStream();
        var replay = ReplayParser.ParseReplay(stream);
        stream.Close();
        
        _context.Replays.Add(replay);
        await _context.SaveChangesAsync();

        return Ok();
    }
    
    
    /// <summary>
    /// Returns a list of replay IDs that match the search query.
    /// </summary>
    /// <param name="mode">The search mode.</param>
    /// <param name="query">The search query.</param>
    [HttpGet]
    [Route("/search")]
    public async Task<ActionResult> SearchReplays(
        [FromQuery] string mode,
        [FromQuery] string query
        )
    {
        var searchMode = SearchMode.Gamemode;
        if (!Enum.TryParse<SearchMode>(mode, true, out var modeEnum))
        {
            // try to parse as a number
            if (int.TryParse(mode, out var modeInt))
            {
                searchMode = (SearchMode) modeInt;
            }
            else
            {
                // LAST TRY: try to parse it as a humanized string
                var humanized = mode.Humanize();
                // Use loop to find the enum value
                var didFind = false;
                foreach (SearchMode value in Enum.GetValues(typeof(SearchMode)))
                {
                    if (value.Humanize().Equals(humanized, StringComparison.OrdinalIgnoreCase))
                    {
                        searchMode = value;
                        didFind = true;
                        break;
                    }
                }

                if (!didFind)
                {
                    // If we still can't find it, return an error
                    return BadRequest($"The search mode '{mode}' is not valid. Valid search modes are: {string.Join(", ", Enum.GetNames(typeof(SearchMode)))}");
                }
            }
        }
        else
        {
            searchMode = modeEnum;
        }

        var replays = await _context.Replays
            .Include(r => r.RoundEndPlayers)
            .OrderByDescending(r => r.Date ?? DateTime.MinValue)
            .ToListAsync();

        replays.Reverse();
        var found = ReplayParser.SearchReplays(searchMode, query, replays);
        return Ok(found);
    }
    
    [HttpGet]
    [Route("/replays")]
    public async Task<ActionResult> GetAllReplays()
    {
        var replays = await _context.Replays.ToListAsync();
        var ids = replays.Select(x => x.Id);
        return Ok(ids);
    }
    
    /// <summary>
    /// Returns the 30 most recent replays.
    /// </summary>
    [HttpGet]
    [Route("/replays/most-recent")]
    public async Task<ActionResult> GetMostRecentReplay()
    {
        var replays = await _context.Replays
            .OrderByDescending(r => r.Date ?? DateTime.MinValue)
            .Take(30)
            .ToListAsync();
        return Ok(replays);
    }
    
    [HttpGet]
    [Route("/replay/{id}")]
    public async Task<ActionResult> GetReplay(int id)
    {
        var replay = await _context.Replays
            .Include(r => r.RoundEndPlayers)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (replay == null)
        {
            return NotFound();
        }

        return Ok(replay);
    }
}
