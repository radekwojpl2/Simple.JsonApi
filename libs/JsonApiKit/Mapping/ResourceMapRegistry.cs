using System.Diagnostics.CodeAnalysis;

namespace JsonApiKit;

/// <summary>Singleton lookup of registered <see cref="ResourceMap{T}"/> instances by entity CLR type
/// (for endpoints building documents) and by JSON:API type string (for fields[type] validation).</summary>
public sealed class ResourceMapRegistry
{
    private readonly Dictionary<Type, IResourceMap> _byEntityType;
    private readonly Dictionary<string, IResourceMap> _byResourceType;

    public ResourceMapRegistry(IEnumerable<IResourceMap> maps)
    {
        _byEntityType = maps.ToDictionary(m => m.EntityType);
        _byResourceType = _byEntityType.Values.ToDictionary(m => m.ResourceType);
    }

    public ResourceMap<T> Get<T>() where T : class =>
        _byEntityType.TryGetValue(typeof(T), out var map)
            ? (ResourceMap<T>)map
            : throw new InvalidOperationException(
                $"No resource map registered for '{typeof(T).Name}'. Register one with AddJsonApi(o => o.AddMap<...>()).");

    public bool TryGet(string resourceType, [NotNullWhen(true)] out IResourceMap? map) =>
        _byResourceType.TryGetValue(resourceType, out map);
}
