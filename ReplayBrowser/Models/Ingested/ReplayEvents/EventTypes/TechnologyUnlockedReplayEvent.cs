using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class TechnologyUnlockedReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<TechnologyUnlockedReplayEvent>
{
    public string Name;

    public string Discipline;

    public int Tier;

    public ReplayEventPlayer Player;

    public void Configure(EntityTypeBuilder<TechnologyUnlockedReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Discipline).IsRequired();
        builder.Property(e => e.Tier).IsRequired();
        builder.Property(e => e.Player).IsRequired();

        builder.Property(e => e.Player)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}