namespace JsonApiKit;

/// <summary>Top-level JSON:API document (https://jsonapi.org/format/#document-structure).
/// Per the spec, <see cref="Data"/> and <see cref="Errors"/> must not coexist in one document.</summary>
public sealed record JsonApiDocument
{
    /// <summary>A single <see cref="ResourceObject"/> or a list of them.</summary>
    public object? Data { get; init; }

    public IReadOnlyList<ResourceObject>? Included { get; init; }

    /// <summary>Pagination links for collection responses (https://jsonapi.org/format/#fetching-pagination).</summary>
    public JsonApiLinks? Links { get; init; }

    public JsonApiMeta? Meta { get; init; }

    /// <summary>Spec error objects ({"errors":[...]}, https://jsonapi.org/format/#errors).</summary>
    public IReadOnlyList<JsonApiError>? Errors { get; init; }
}
