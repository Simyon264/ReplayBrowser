namespace Server.ReplayLoading;

/// <summary>
/// Represents a replay provider that can retrieve replay files from a directory.
/// This will never add any replays to the queue. It is used to temporarily disable some sources.
/// </summary>
[ReplayProviderName("dummy")]
public class DummyProvider : ReplayProvider
{
    public override Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token)
    {
        return Task.CompletedTask;
    }
}