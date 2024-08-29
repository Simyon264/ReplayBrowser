using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;
using YamlDotNet.Serialization;

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
    [JsonIgnore]
    public MobState OldState;

    [YamlIgnore]
    [NotMapped]
    public string OldStateString
    {
        get => OldState.ToString();
        set => OldState = Enum.Parse<MobState>(value);
    }

    /// <summary>
    /// The new state of the mob.
    /// </summary>
    [JsonIgnore]
    public MobState NewState;

    [YamlIgnore]
    [NotMapped]
    public string NewStateString
    {
        get => NewState.ToString();
        set => NewState = Enum.Parse<MobState>(value);
    }

    public void Configure(EntityTypeBuilder<MobStateChangedNPCReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Target).IsRequired();
        builder.Property(e => e.OldState).IsRequired();
        builder.Property(e => e.NewState).IsRequired();
    }
}
