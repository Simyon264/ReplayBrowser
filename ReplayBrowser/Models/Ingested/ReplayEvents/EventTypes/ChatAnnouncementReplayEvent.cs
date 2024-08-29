using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ChatAnnouncementReplayEvent : ReplayDbEvent
{
    public string Message;

    public string Sender;
}