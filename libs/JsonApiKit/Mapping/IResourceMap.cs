namespace JsonApiKit;

/// <summary>Untyped view of a resource map, used by the registry and query validation.</summary>
public interface IResourceMap
{
    Type EntityType { get; }

    /// <summary>JSON:API resource type string, e.g. "contacts".</summary>
    string ResourceType { get; }

    /// <summary>Whether <paramref name="name"/> is a declared attribute or relationship (a spec "field").</summary>
    bool HasField(string name);

    /// <summary>All declared field names (attributes + relationships), e.g. for documentation.</summary>
    IReadOnlyCollection<string> Fields { get; }
}
