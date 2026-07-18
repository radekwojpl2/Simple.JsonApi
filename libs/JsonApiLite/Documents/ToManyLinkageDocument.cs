namespace JsonApiLite;

/// <summary>Document for a to-many relationship endpoint: 'data' is the complete member set,
/// possibly empty.</summary>
public sealed record ToManyLinkageDocument
{
    public required IReadOnlyList<ResourceIdentifier> Data { get; init; }

    public Links? Links { get; init; }
}
