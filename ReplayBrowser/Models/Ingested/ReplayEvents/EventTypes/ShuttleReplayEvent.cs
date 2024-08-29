using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ShuttleReplayEvent : ReplayDbEvent
{
    public int? Countdown;

    public ReplayEventPlayer? Source;
}