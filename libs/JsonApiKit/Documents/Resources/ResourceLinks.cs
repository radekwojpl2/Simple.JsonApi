namespace JsonApiKit;

/// <summary>Resource-level links object; self locates the resource itself
/// (https://jsonapi.org/format/#document-resource-object-links).</summary>
public sealed record ResourceLinks(string Self);
