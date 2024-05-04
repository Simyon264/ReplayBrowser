namespace Server.ReplayLoading;

public abstract class ReplayProvider
{
    public abstract Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token);
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