using ReplayBrowser.Data.Models.Account;

namespace ReplayBrowser.Data;

public class AccountHistoryResponse
{
    public List<HistoryEntry> History { get; set; }
    
    public int Page { get; set; }
    public int TotalPages { get; set; }
}