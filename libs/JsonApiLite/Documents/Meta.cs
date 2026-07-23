using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A meta object (https://jsonapi.org/format/#document-meta). The spec reserves no member
/// names — "any members MAY be specified" — so the shape is the endpoint's own: build one as
/// <see cref="Meta{TMeta}"/> to declare it, and read it back with <see cref="As{T}"/>. Positions
/// that hold meta store this base type, because a type parameter on <see cref="Link"/> would
/// cascade into <see cref="Links"/> and everything holding one; documents, which have no such
/// problem, take the meta type as a parameter of their own instead.</summary>
[JsonConverter(typeof(MetaConverter))]
public record Meta : IMeta
{
    /// <summary>The members as they stand on the wire — what a server sent, whether or not any
    /// declared type describes it.</summary>
    public JsonObject Members { get; init; } = [];

    /// <summary>Reads the members as a declared type. Throws <see cref="JsonException"/> if they
    /// do not fit it — the same contract as reading any other malformed document.</summary>
    public T? As<T>(JsonSerializerOptions? options = null) =>
        Members.Deserialize<T>(options ?? JsonApiSerializer.Options);

    /// <summary>Records compare by value, and so should the members: <see cref="JsonObject"/>
    /// itself compares by reference.</summary>
    public virtual bool Equals(Meta? other) => other is not null && JsonNode.DeepEquals(Members, other.Members);

    public override int GetHashCode() => Members.ToJsonString().GetHashCode();
}

/// <summary>Meta whose shape is declared: <c>new Meta&lt;PageMeta&gt;(new PageMeta(Total: 2))</c>.
/// The value is written as its own members, so the wire is identical to having built the object
/// by hand. Reading gives back the base <see cref="Meta"/> — the wire carries no type name —
/// so recover the shape with <see cref="Meta.As{T}"/>.</summary>
public sealed record Meta<TMeta> : Meta where TMeta : class, IMeta
{
    public Meta(TMeta value, JsonSerializerOptions? options = null)
    {
        Value = value;
        Members = JsonSerializer.SerializeToNode(value, options ?? JsonApiSerializer.Options)
            ?.AsObject() ?? [];
    }

    public TMeta Value { get; }
}
