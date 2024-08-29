using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ShuttleReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<ShuttleReplayEvent>
{
    public int? Countdown;

    public ReplayEventPlayer? Source;

    public void Configure(EntityTypeBuilder<ShuttleReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Countdown);
        builder.Property(e => e.Source);

        builder.Property(e => e.Source)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}