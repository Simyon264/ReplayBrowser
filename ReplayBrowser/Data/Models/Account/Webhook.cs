namespace ReplayBrowser.Data.Models.Account;

public class Webhook
{
    public int Id { get; set; }

    public string Url { get; set; }
    public WebhookType Type { get; set; }
    public List<WebhookHistory> Logs { get; set; } = new();
}