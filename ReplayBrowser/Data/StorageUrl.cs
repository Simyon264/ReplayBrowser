namespace ReplayBrowser.Data;

public class StorageUrl
{
    public required string Url { get; set; }
    public required string Provider { get; set; }
    
    public required string FallBackServerName { get; set; } 
    public required string FallBackServerId { get; set; }
    
    public override string ToString()
    {
        return Url;
    }
}