namespace JsonApiKit;

/// <summary>Relationship object whose <see cref="Data"/> is resource linkage:
/// a single <see cref="ResourceIdentifier"/> (to-one) or a list of them (to-many).</summary>
public sealed record Relationship
{
    public RelationshipLinks? Links { get; init; }

    public object? Data { get; init; }

    public static Relationship ToOne(string type, string id, RelationshipLinks? links = null) =>
        new() { Data = new ResourceIdentifier(type, id), Links = links };

    public static Relationship ToMany(string type, IEnumerable<string> ids, RelationshipLinks? links = null) =>
        new() { Data = ids.Select(id => new ResourceIdentifier(type, id)).ToList(), Links = links };
}
