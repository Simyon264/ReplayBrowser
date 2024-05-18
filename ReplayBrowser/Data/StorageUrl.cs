namespace ReplayBrowser.Data;

public class StorageUrl
{
    public string Url { get; set; }
    public string Provider { get; set; }

    public override string ToString()
    {
        return Url;
    }
}