namespace JsonApiLite;

/// <summary>Error document ({"errors":[...]}, https://jsonapi.org/format/#errors). Per the spec
/// it never carries primary data.</summary>
public sealed record ErrorDocument
{
    public required IReadOnlyList<Error> Errors { get; init; }

    public Links? Links { get; init; }

    public Meta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}
