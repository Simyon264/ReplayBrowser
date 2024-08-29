using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class StoreBuyReplayEvent : ReplayDbEvent
{
    public ReplayEventPlayer Buyer;

    public string Item;

    public int Cost;
}