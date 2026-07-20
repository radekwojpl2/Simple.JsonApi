namespace JsonApiLite;

/// <summary>Document whose primary data is a resource collection — a list response, with
/// pagination in <see cref="Links"/> and <see cref="Meta"/> when the endpoint pages. Prefer
/// <see cref="ResourceCollectionDocument{TAttributes, TRelationships}"/> when the resource's
/// relationship names are known at compile time.</summary>
public sealed record ResourceCollectionDocument<TAttributes> where TAttributes : class
{
    public required IReadOnlyList<Resource<TAttributes>> Data { get; init; }

    public IReadOnlyList<Resource>? Included { get; init; }

    public Links? Links { get; init; }

    public Meta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}

/// <summary>Collection document with the relationships typed as well; see
/// <see cref="ResourceDocument{TAttributes, TRelationships, TMeta}"/>.</summary>
/// <typeparam name="TMeta">The document's meta shape — on a list endpoint, usually where the
/// page counts live. The spec names no meta members, so the shape is the endpoint's own.</typeparam>
public record ResourceCollectionDocument<TAttributes, TRelationships, TMeta>
    where TAttributes : class, IAttributes
    where TRelationships : class, IRelationships
    where TMeta : class, IMeta
{
    public required IReadOnlyList<Resource<TAttributes, TRelationships>> Data { get; init; }

    public IReadOnlyList<Resource>? Included { get; init; }

    public Links? Links { get; init; }

    public TMeta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}

/// <summary>The same document with meta left as the built-in <see cref="JsonApiLite.Meta"/>;
/// see <see cref="ResourceDocument{TAttributes, TRelationships}"/>.</summary>
public sealed record ResourceCollectionDocument<TAttributes, TRelationships>
    : ResourceCollectionDocument<TAttributes, TRelationships, Meta>
    where TAttributes : class, IAttributes
    where TRelationships : class, IRelationships;
