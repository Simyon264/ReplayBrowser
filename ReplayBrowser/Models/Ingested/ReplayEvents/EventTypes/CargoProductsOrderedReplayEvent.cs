using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class CargoProductsOrderedReplayEvent : ReplayDbEvent
{
    public ReplayEventPlayer ApprovedBy;

    public CargoReplayProduct Product;
}

public class CargoReplayProduct
{
    public string ProductId;

    public string Reason = "";

    public ReplayEventPlayer OrderedBy;
}