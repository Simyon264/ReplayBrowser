namespace ReplayBrowser.Data.Models.Account;

public class Webhook
{
    public int Id { get; set; }

    public string Url { get; set; } = null!;
    public WebhookType Type { get; set; }
    public List<WebhookHistory> Logs { get; set; } = new();

    /// <summary>
    /// Comma seperated list of servers that this webhook is allowed to be called from.
    /// </summary>
    /// <remarks>
    /// It's comma seperated because I cannot be bothered to make the UI for this. An ideal solution would be a list of strings.
    /// </remarks>
    public string Servers { get; set; } = null!;
}