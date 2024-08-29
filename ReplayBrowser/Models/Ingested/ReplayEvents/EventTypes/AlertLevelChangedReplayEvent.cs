using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class AlertLevelChangedReplayEvent : ReplayDbEvent
{
    public string AlertLevel;
}