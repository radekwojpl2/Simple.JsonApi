using System.Text.Json.Serialization;

namespace JsonApiKit;

/// <summary>Document for relationship and related-resource endpoints. Primary data is a
/// <see cref="ResourceIdentifier"/> (relationship linkage), a <see cref="ResourceObject"/>
/// (related resource), or null for an empty to-one — emitted explicitly, because a document
/// without data, errors, or meta would be invalid.</summary>
public sealed record JsonApiToOneDocument
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Data { get; init; }

    public JsonApiLinks? Links { get; init; }
}
