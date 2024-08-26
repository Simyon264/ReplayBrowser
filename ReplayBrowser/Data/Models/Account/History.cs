namespace ReplayBrowser.Data.Models.Account;

public class HistoryEntry
{
    public int Id { get; set; }

    public required string Action { get; set; }
    public DateTime Time { get; set; }
    public string? Details { get; set; }

    public Account? Account { get; set; } = null!;
    public int? AccountId { get; set; }
}

public enum Action
{
    // Account actions
    AccountSettingsChanged,
    Login,
    WebhooksChanged,

    // Site actions
    SearchPerformed,
    LeaderboardViewed,
    ProfileViewed,
    MainPageViewed,
}