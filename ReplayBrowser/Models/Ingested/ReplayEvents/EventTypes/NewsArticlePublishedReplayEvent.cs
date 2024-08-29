using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class NewsArticlePublishedReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<NewsArticlePublishedReplayEvent>
{
    public string Title;

    public string Content;

    public string? Author;

    public TimeSpan ShareTime;
    public void Configure(EntityTypeBuilder<NewsArticlePublishedReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.Author);
        builder.Property(e => e.ShareTime).IsRequired();
    }
}
