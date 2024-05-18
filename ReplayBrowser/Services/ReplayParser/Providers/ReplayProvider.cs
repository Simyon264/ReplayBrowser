namespace ReplayBrowser.Services.ReplayParser.Providers;

public abstract class ReplayProvider
{
    public abstract Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token);
    
    public HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ReplayBrowser");
        return httpClient;
    }
    
    public ReplayProvider(ReplayParserService replayParserService)
    {
        ReplayParserService = replayParserService;
    }
    
    protected ReplayParserService ReplayParserService { get; }
}

/// <summary>
/// 
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ReplayProviderNameAttribute : Attribute
{
    public string Name { get; }

    public ReplayProviderNameAttribute(string name)
    {
        Name = name;
    }
}