using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

/// <summary>
/// Represents a player controlled mob changing mob states.
/// </summary>
public class MobStateChangedPlayerReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<MobStateChangedPlayerReplayEvent>
{
    /// <summary>
    /// The target of the mob state change.
    /// </summary>
    public ReplayEventPlayer Target;

    /// <summary>
    /// The old state of the mob.
    /// </summary>
    public MobState OldState;

    /// <summary>
    /// The new state of the mob.
    /// </summary>
    public MobState NewState;

    public void Configure(EntityTypeBuilder<MobStateChangedPlayerReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Target).IsRequired();
        builder.Property(e => e.OldState).IsRequired();
        builder.Property(e => e.NewState).IsRequired();

        builder.Property(e => e.Target)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}
