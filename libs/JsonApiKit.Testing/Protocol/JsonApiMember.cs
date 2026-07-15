namespace JsonApiKit.Testing;

/// <summary>JSON:API document member names (https://jsonapi.org/format/#document-structure),
/// for navigating response JSON without magic strings.</summary>
public static class JsonApiMember
{
    public const string Data = "data";
    public const string Id = "id";
    public const string Type = "type";
    public const string Attributes = "attributes";
    public const string Relationships = "relationships";
    public const string Links = "links";
    public const string Included = "included";
    public const string Meta = "meta";
    public const string Errors = "errors";
    public const string Self = "self";
    public const string Related = "related";
    public const string First = "first";
    public const string Prev = "prev";
    public const string Next = "next";
    public const string Last = "last";

    /// <summary>Conventional (non-spec) pagination meta members emitted by JsonApiKit.</summary>
    public const string Total = "total";

    /// <inheritdoc cref="Total"/>
    public const string PageCount = "pageCount";
}
