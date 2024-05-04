using System.Reflection;

namespace Server.ReplayLoading;

public class ReplayProviderFactory
{
    public static ReplayProvider GetProvider(string providerName)
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

        return (ReplayProvider) Activator.CreateInstance(type)!;
    }
}