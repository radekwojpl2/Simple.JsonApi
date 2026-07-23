namespace JsonApiLite;

/// <summary>Resource identifier object: the (type, id) pair that is resource linkage
/// (https://jsonapi.org/format/#document-resource-identifier-objects).</summary>
public sealed record ResourceIdentifier(string Type, string Id)
{
    /// <summary>Identifier-level meta — information about the linkage rather than about either
    /// resource it names.</summary>
    public Meta? Meta { get; init; }

    /// <summary>An identifier with the type name pulled from the attributes type's
    /// <see cref="IResourceType"/> declaration.</summary>
    public static ResourceIdentifier Of<TAttributes>(string id) where TAttributes : IResourceType =>
        new(TAttributes.ResourceType, id);
}
