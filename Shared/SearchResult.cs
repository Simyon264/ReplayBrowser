using Shared.Models;

namespace Shared;

public class SearchResult
{
    public int PageCount { get; set; }
    public int CurrentPage { get; set; }
    public List<Replay> Replays { get; set; }
    public int TotalReplays { get; set; }
}