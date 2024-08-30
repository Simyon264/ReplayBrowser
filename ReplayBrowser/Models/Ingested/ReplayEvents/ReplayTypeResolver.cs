using System.Reflection;
using ReplayBrowser.Data.Models;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.BufferedDeserialization.TypeDiscriminators;

namespace ReplayBrowser.Models.Ingested.ReplayEvents;

/// <summary>
/// Resolves the type of a replay event based on its name.
/// </summary>
public class ReplayTypeResolver : INodeTypeResolver
{
    private readonly Dictionary<string, Type> _typeMappings;

    public ReplayTypeResolver()
    {
        _typeMappings = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ReplayDbEvent)))
            .ToDictionary(t => t.Name, t => t);
    }

    public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
    {
        if (nodeEvent is Scalar scalar && scalar.Value.StartsWith("!type:"))
        {
            var typeName = scalar.Value.Substring(6); // Extract the type name after '!type:'
            if (_typeMappings.TryGetValue(typeName, out var resolvedType))
            {
                currentType = resolvedType;
                return true;
            }
        }

        return false;
    }
}