using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

/// <summary>
/// Represents a player controlled mob changing mob states.
/// </summary>
public class MobStateChangedPlayerReplayEvent : ReplayDbEvent
{
    public ReplayEventPlayer Target;

    public MobState OldState;

    public MobState NewState;
}