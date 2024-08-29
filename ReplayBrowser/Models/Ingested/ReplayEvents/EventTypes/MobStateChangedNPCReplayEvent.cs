using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

/// <summary>
/// Represents a non-player controlled mob changing mob states.
/// </summary>
public class MobStateChangedNPCReplayEvent : ReplayDbEvent
{
    public string Target;

    public MobState OldState;

    public MobState NewState;
}