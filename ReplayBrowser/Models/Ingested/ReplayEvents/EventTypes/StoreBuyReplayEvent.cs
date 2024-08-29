using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class StoreBuyReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<StoreBuyReplayEvent>
{
    public ReplayEventPlayer Buyer;

    public string Item;

    public int Cost;

    public void Configure(EntityTypeBuilder<StoreBuyReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Buyer).IsRequired();
        builder.Property(e => e.Item).IsRequired();
        builder.Property(e => e.Cost).IsRequired();

        builder.Property(e => e.Buyer)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}