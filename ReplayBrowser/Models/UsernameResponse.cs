namespace ReplayBrowser.Models;

public class UsernameResponse
{
    public required string userName { get; set; }

    public required string userId { get; set; }

    public string? patronTier { get; set; }

    public DateTime createdTime { get; set; }
}