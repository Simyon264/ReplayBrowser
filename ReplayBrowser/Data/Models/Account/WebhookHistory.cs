namespace ReplayBrowser.Data.Models.Account;

/// <summary>
/// Represents a history entry for a webhook. Contains information about when something was sent and the response.
/// </summary>
public class WebhookHistory
{
    public int Id { get; set; }

    public DateTime SentAt { get; set; }
    public int ResponseCode { get; set; }
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength // This is a log, it can be as long as it wants.
    public string ResponseBody { get; set; } = string.Empty;
}