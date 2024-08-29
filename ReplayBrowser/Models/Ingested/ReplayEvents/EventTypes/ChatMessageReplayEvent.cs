using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ChatMessageReplayEvent : ReplayDbEvent
{
    public string Message;

    public ReplayEventPlayer Sender;

    public string Type;
}