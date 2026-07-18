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
}

/// <summary>Collection document with the relationships typed as well; see
/// <see cref="ResourceDocument{TAttributes, TRelationships}"/>.</summary>
public sealed record ResourceCollectionDocument<TAttributes, TRelationships>
    where TAttributes : class
    where TRelationships : class
{
    public required IReadOnlyList<Resource<TAttributes, TRelationships>> Data { get; init; }

    public IReadOnlyList<Resource>? Included { get; init; }

    public Links? Links { get; init; }

    public Meta? Meta { get; init; }
}
