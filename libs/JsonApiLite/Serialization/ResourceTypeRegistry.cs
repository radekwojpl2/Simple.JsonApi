using System.Diagnostics.CodeAnalysis;

namespace JsonApiLite;

/// <summary>Maps resource type names to concrete resource CLR types, so a compound document's
/// included resources deserialize strongly typed instead of as <c>Resource&lt;JsonObject&gt;</c>.
/// Pass one to <see cref="JsonApiSerializer.CreateOptions"/>; unmapped types still fall back to
/// <c>Resource&lt;JsonObject&gt;</c>.</summary>
public sealed class ResourceTypeRegistry
{
    private readonly Dictionary<string, Type> _resourceTypes = [];

    public ResourceTypeRegistry Map<TAttributes, TRelationships>(string type)
        where TAttributes : class
        where TRelationships : class
    {
        _resourceTypes[type] = typeof(Resource<TAttributes, TRelationships>);
        return this;
    }

    public ResourceTypeRegistry Map<TAttributes>(string type) where TAttributes : class
    {
        _resourceTypes[type] = typeof(Resource<TAttributes>);
        return this;
    }

    /// <summary>Maps under the name the attributes type declares via <see cref="IResourceType"/>,
    /// so the registry cannot drift from the declaration.</summary>
    public ResourceTypeRegistry Map<TAttributes, TRelationships>()
        where TAttributes : class, IResourceType
        where TRelationships : class =>
        Map<TAttributes, TRelationships>(TAttributes.ResourceType);

    /// <summary>Same for the dictionary-relationships flavor.</summary>
    public ResourceTypeRegistry Map<TAttributes>() where TAttributes : class, IResourceType =>
        Map<TAttributes>(TAttributes.ResourceType);

    internal bool TryResolve(string type, [NotNullWhen(true)] out Type? resourceType) =>
        _resourceTypes.TryGetValue(type, out resourceType);
}
