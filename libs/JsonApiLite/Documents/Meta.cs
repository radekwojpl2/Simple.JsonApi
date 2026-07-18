using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Document meta. Pagination gets first-class members — total resources and page count,
/// the shape responses here carry — and anything else round-trips through
/// <see cref="Additional"/>, since the spec leaves meta free-form.</summary>
[JsonConverter(typeof(MetaConverter))]
public sealed record Meta(int? Total = null, int? PageCount = null)
{
    /// <summary>Members beyond the typed pagination pair, written inline into the meta object.</summary>
    public JsonObject? Additional { get; init; }
}
