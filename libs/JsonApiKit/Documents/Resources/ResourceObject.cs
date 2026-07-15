namespace JsonApiKit;

public sealed record ResourceObject(string Type, string Id)
{
    /// <summary>An anonymous object or a name-to-value dictionary (what <see cref="ResourceMap{T}.Build(T, IReadOnlySet{string}?, object?)"/> produces).</summary>
    public object? Attributes { get; init; }

    public Dictionary<string, Relationship>? Relationships { get; init; }

    public ResourceLinks? Links { get; init; }
}
