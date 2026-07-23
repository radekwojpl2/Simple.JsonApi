namespace JsonApiLite;

/// <summary>Links object; members that are null are omitted from the wire format. Self/Related
/// serve resources and relationships, About serves error objects, First/Prev/Next/Last serve
/// pagination (https://jsonapi.org/format/#document-links). Each member is a <see cref="Link"/>:
/// assign a string for a bare URI, or construct a <see cref="Link"/> when it carries meta.</summary>
public sealed record Links
{
    public Link? Self { get; init; }
    public Link? Related { get; init; }
    public Link? About { get; init; }
    public Link? First { get; init; }
    public Link? Prev { get; init; }
    public Link? Next { get; init; }
    public Link? Last { get; init; }
}
