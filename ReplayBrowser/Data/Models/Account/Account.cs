namespace ReplayBrowser.Data.Models.Account;

public class Account
{
    // Primary key
    public int Id { get; set; }
    
    public Guid Guid { get; set; }
    public string Username { get; set; }
    public bool IsAdmin { get; set; } = false;
    public AccountSettings Settings { get; set; } = new();
    
    /// <summary>
    /// Replays that the user has favorited.
    /// </summary>
    public List<int> FavoriteReplays { get; set; } = new();
    
    /// <summary>
    /// Profiles that the user "watches".
    /// </summary>
    public List<Guid> SavedProfiles { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();
    
    public bool Protected { get; set; } = false;
}