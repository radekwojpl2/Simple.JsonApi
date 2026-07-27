using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Document whose primary data is a single resource
/// (https://jsonapi.org/format/#document-structure): a create/update request body, or a
/// single-resource response. <see cref="Data"/> is written even when null, because a document
/// without a data member is not a resource document at all. Prefer
/// <see cref="ResourceDocument{TAttributes, TRelationships}"/> when the resource's relationship
/// names are known at compile time.</summary>
public sealed record ResourceDocument<TAttributes> where TAttributes : class
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required Resource<TAttributes>? Data { get; init; }

    /// <summary>Compound-document resources (https://jsonapi.org/format/#document-compound-documents).
    /// Heterogeneous, so typed as the abstract <see cref="Resource"/>: each element is some
    /// concrete resource when writing, and reads back as <c>Resource&lt;JsonObject&gt;</c>.</summary>
    public IReadOnlyList<Resource>? Included { get; init; }

    public Links? Links { get; init; }

    public Meta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}

/// <summary>Single-resource document with the relationships typed as well: the resource is a
/// <see cref="Resource{TAttributes, TRelationships}"/>, so relationship access is by member
/// rather than by name. Prefer this over <see cref="ResourceDocument{TAttributes}"/> whenever
/// the resource's relationship names are known at compile time.</summary>
/// <typeparam name="TMeta">The document's meta shape. The spec reserves no meta member names, so
/// this is entirely the endpoint's own: declare a record for what it actually sends.</typeparam>
/// <typeparam name="TIncluded">The document's sideload shape — one member per resource type the
/// document may sideload, so a sideloaded resource is reached by member rather than by cast.
/// Defaults to <see cref="AnyIncluded"/>, which leaves the member untyped.</typeparam>
public record ResourceDocument<TAttributes, TRelationships, TMeta, TIncluded>
    where TAttributes : class, IAttributes
    where TRelationships : class, IRelationships
    where TMeta : class, IMeta
    where TIncluded : class, IIncluded
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required Resource<TAttributes, TRelationships>? Data { get; init; }

    /// <summary>Compound-document resources (https://jsonapi.org/format/#document-compound-documents),
    /// bucketed into <typeparamref name="TIncluded"/>'s members by resource type. One flat array on
    /// the wire whatever the declaration — the shape is a reading convenience, not a wire
    /// change.</summary>
    public TIncluded? Included { get; init; }

    public Links? Links { get; init; }

    public TMeta? Meta { get; init; }

    public JsonApiObject? JsonApi { get; init; }
}

/// <summary>The same document with the sideload member left untyped. C# has no default type
/// arguments, so the default is spelled as a subtype; name TIncluded explicitly — which means
/// naming TMeta too, even if only to leave it as <see cref="JsonApiLite.Meta"/> — to declare the
/// resource types the document may sideload.</summary>
public record ResourceDocument<TAttributes, TRelationships, TMeta>
    : ResourceDocument<TAttributes, TRelationships, TMeta, AnyIncluded>
    where TAttributes : class, IAttributes
    where TRelationships : class, IRelationships
    where TMeta : class, IMeta;

/// <summary>The same document with meta left as the built-in <see cref="JsonApiLite.Meta"/>.
/// C# has no default type arguments, so the default is spelled as a subtype; name TMeta
/// explicitly to type the meta object too.</summary>
public sealed record ResourceDocument<TAttributes, TRelationships>
    : ResourceDocument<TAttributes, TRelationships, Meta>
    where TAttributes : class, IAttributes
    where TRelationships : class, IRelationships;
