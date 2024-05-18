using System.Reflection;

namespace ReplayBrowser.Services.ReplayParser.Providers;

public static class ReplayProviderFactory
{
    public static ReplayProvider GetProvider(string providerName, ReplayParserService caller)
    {
        var type = Assembly.GetExecutingAssembly().GetTypes().FirstOrDefault(t => t.GetCustomAttribute<ReplayProviderNameAttribute>()?.Name == providerName);
        if (type == null)
        {
            throw new ArgumentException("Invalid provider name.");
        }
        
        if (!typeof(ReplayProvider).IsAssignableFrom(type))
        {
            throw new ArgumentException("Invalid provider type.");
        }

        return (ReplayProvider) Activator.CreateInstance(type, caller)!;
    }
}