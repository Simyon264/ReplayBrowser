using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class AlertLevelChangedReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<AlertLevelChangedReplayEvent>
{
    public string AlertLevel;
    public void Configure(EntityTypeBuilder<AlertLevelChangedReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.AlertLevel).IsRequired();
    }
}