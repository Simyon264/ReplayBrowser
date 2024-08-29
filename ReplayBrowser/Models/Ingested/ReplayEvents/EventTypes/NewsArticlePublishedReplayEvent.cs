using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class NewsArticlePublishedReplayEvent : ReplayDbEvent
{
    public string Title;

    public string Content;

    public string? Author;

    public TimeSpan ShareTime;
}