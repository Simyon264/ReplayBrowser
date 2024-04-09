using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using System.Collections.Generic;
using Server.Helpers;

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
        [FromQuery] string query,
        [FromQuery] int page = 0
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
        
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("The search query cannot be empty.");
        }
        
        if (page < 0)
        {
            return BadRequest("The page number cannot be negative.");
        }

        var found = ReplayParser.SearchReplays(searchMode, query, _context, page, Constants.ReplaysPerPage);
        
        var pageCount = Paginator.GetPageCount(found.Item2, Constants.ReplaysPerPage);
        
        return Ok(new SearchResult()
        {
            Replays = found.Item1,
            PageCount = pageCount,
            CurrentPage = page,
            TotalReplays = found.Item2
        });
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
    /// Returns the most recent replays. Not sorted by date. Just the most recent replays stored in the database.
    /// </summary>
    [HttpGet]
    [Route("/replays/most-recent")]
    public async Task<ActionResult> GetMostRecentReplay()
    {
        var replays = await _context.Replays
            .OrderByDescending(r => r.Id)
            .Include(r => r.RoundEndPlayers)
            .Take(32)
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
