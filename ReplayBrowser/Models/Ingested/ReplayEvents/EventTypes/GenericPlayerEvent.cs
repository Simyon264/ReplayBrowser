using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class GenericPlayerEvent : ReplayDbEvent
{
    /// <summary>
    /// The player info associated with this event. This who this event is about.
    /// </summary>
    public ReplayEventPlayer Target { get; set; }

    /// <summary>
    /// The source of the event. Who was the cause for the Target being affected? Can be null.
    /// </summary>
    public ReplayEventPlayer? Origin { get; set; }
}