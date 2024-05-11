using Shared.Models.Account;

namespace Shared;

public class AccountHistoryResponse
{
    public List<HistoryEntry> History { get; set; }
    
    public int Page { get; set; }
    public int TotalPages { get; set; }
}