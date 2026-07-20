using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Document for a to-one relationship endpoint
/// (https://jsonapi.org/format/#crud-updating-to-one-relationships): 'data' is a single
/// identifier, or null for an empty relationship — written explicitly, since omission would make
/// the document invalid.</summary>
public sealed record ToOneLinkageDocument
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required ResourceIdentifier? Data { get; init; }

    public Links? Links { get; init; }

    public Meta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}
