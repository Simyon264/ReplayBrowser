namespace ReplayBrowser.Data.Models.Account;

public class Account
{
    // Primary key
    public int Id { get; set; }
    
    public Guid Guid { get; set; }
    public string Username { get; set; }
    public bool IsAdmin { get; set; } = false;
    public AccountSettings Settings { get; set; } = new();
    
    public List<HistoryEntry> History { get; set; } = new();
}