using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

/// <summary>
/// Represents a non-player controlled mob changing mob states.
/// </summary>
public class MobStateChangedNPCReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<MobStateChangedNPCReplayEvent>
{
    /// <summary>
    /// The target of the mob state change.
    /// </summary>
    public string Target;

    /// <summary>
    /// The old state of the mob.
    /// </summary>
    public MobState OldState;

    /// <summary>
    /// The new state of the mob.
    /// </summary>
    public MobState NewState;

    public void Configure(EntityTypeBuilder<MobStateChangedNPCReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Target).IsRequired();
        builder.Property(e => e.OldState).IsRequired();
        builder.Property(e => e.NewState).IsRequired();
    }
}
