using System.Text.RegularExpressions;

namespace ReplayBrowser.Models;

public class StorageUrl
{
    public required string Url { get; set; }
    public required string Provider { get; set; }

    public required string FallBackServerName { get; set; }
    public required string FallBackServerId { get; set; }

    public required string ReplayRegex { get; set; }
    public required string ServerNameRegex { get; set; }


    // Gets set via CompileRegex in GetStorageUrlFromReplayLink, the "only" way to access this class
    public Regex ReplayRegexCompiled { get; set; } = null!;
    public Regex ServerNameRegexCompiled { get; set; } = null!;

    public void CompileRegex()
    {
        ReplayRegexCompiled = new Regex(ReplayRegex);
        ServerNameRegexCompiled = new Regex(ServerNameRegex);
    }

    public override string ToString()
    {
        return Url;
    }
}