using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models;

public class SearchResult
{
    /// <summary>
    /// If the result was fetched from the cache.
    /// </summary>
    public bool IsCache { get; set; } = false;
    public int PageCount { get; set; } = 1;
    public int CurrentPage { get; set; } = 1;
    public List<ReplayResult> Replays { get; set; } = [];
    public int TotalReplays { get; set; } = 0;
    public List<SearchQueryItem> SearchItems { get; set; } = [];
}