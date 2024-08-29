using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class GenericObjectEvent : ReplayDbEvent
{
    public string Target;

    public string? Origin;
}