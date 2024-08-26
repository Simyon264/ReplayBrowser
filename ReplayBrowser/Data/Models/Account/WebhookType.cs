namespace ReplayBrowser.Data.Models.Account;

public enum WebhookType : byte
{
    /// <summary>
    /// Will attempt to send the replay data to a Discord webhook.
    /// </summary>
    Discord,

    /// <summary>
    /// Will send collected data to a URL.
    /// </summary>
    Json,
}