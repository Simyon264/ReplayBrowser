using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class CargoObjectSoldReplayEvent : ReplayDbEvent
{
    /// <summary>
    /// The amount of money the objects were sold for
    /// </summary>
    public double Amount;

    public int ObjectsSold;
}