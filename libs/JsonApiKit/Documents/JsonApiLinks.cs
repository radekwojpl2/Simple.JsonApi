namespace JsonApiKit;

/// <summary>Top-level links object: self plus either pagination links (collections) or a related
/// link (relationship documents). Null members are omitted from the wire format.</summary>
public sealed record JsonApiLinks(string? Self = null, string? Related = null, string? First = null,
    string? Prev = null, string? Next = null, string? Last = null);
