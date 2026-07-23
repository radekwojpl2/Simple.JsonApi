namespace JsonApiLite;

/// <summary>The 'jsonapi' object (https://jsonapi.org/format/#document-jsonapi-object): the
/// server's statement about the protocol itself rather than about the data — which version it
/// implements, which extensions and profiles are in play, and meta about any of that. Every
/// member is optional, so an empty object is valid and means only "this is JSON:API".</summary>
public sealed record JsonApiObject
{
    /// <summary>The highest JSON:API version supported, e.g. "1.1".</summary>
    public string? Version { get; init; }

    /// <summary>URIs of the extensions in use.</summary>
    public IReadOnlyList<string>? Ext { get; init; }

    /// <summary>URIs of the profiles in use.</summary>
    public IReadOnlyList<string>? Profile { get; init; }

    public Meta? Meta { get; init; }
}
