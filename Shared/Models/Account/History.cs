namespace Shared.Models.Account;

public class HistoryEntry
{
    public int Id { get; set; }
    
    public string Action { get; set; }
    public DateTime Time { get; set; }
    public string? Details { get; set; }
}

public enum Action
{
    // Account actions
    AccountSettingsChanged,
    
    // Site actions
    SearchPerformed,
    LeaderboardViewed,
    ProfileViewed,
    MainPageViewed,
}