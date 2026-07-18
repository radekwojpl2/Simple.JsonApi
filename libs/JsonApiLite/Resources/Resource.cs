using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A resource object (https://jsonapi.org/format/#document-resource-objects) without its
/// attributes or relationships typed: what every resource shares. This is the element type of a
/// compound document's included list, where resources are heterogeneous — each element is some
/// concrete resource, written with its concrete shape and read back as
/// <c>Resource&lt;JsonObject&gt;</c> (the caller knows from <see cref="Type"/> which attributes
/// type to deserialize the <see cref="JsonObject"/> into) — or into its registered concrete type
/// when the options carry a <see cref="ResourceTypeRegistry"/>.</summary>
[JsonConverter(typeof(ResourceConverter))]
public abstract record Resource
{
    [JsonPropertyOrder(-2)]
    public required string Type { get; init; }

    [JsonPropertyOrder(-1)]
    public string? Id { get; init; }

    [JsonPropertyOrder(3)]
    public Links? Links { get; init; }

    /// <summary>A fully typed resource with <see cref="Type"/> filled from the attributes type's
    /// <see cref="IResourceType"/> declaration. The object-initializer form stays available for
    /// attribute types that do not declare a resource type name.</summary>
    public static Resource<TAttributes, TRelationships> Create<TAttributes, TRelationships>(
        string? id = null, TAttributes? attributes = null, TRelationships? relationships = null,
        Links? links = null)
        where TAttributes : class, IResourceType
        where TRelationships : class =>
        new()
        {
            Type = TAttributes.ResourceType,
            Id = id,
            Attributes = attributes,
            Relationships = relationships,
            Links = links,
        };

    /// <summary>Same for the dictionary-relationships flavor.</summary>
    public static Resource<TAttributes> Create<TAttributes>(
        string? id = null, TAttributes? attributes = null,
        IReadOnlyDictionary<string, Relationship>? relationships = null, Links? links = null)
        where TAttributes : class, IResourceType =>
        new()
        {
            Type = TAttributes.ResourceType,
            Id = id,
            Attributes = attributes,
            Relationships = relationships,
            Links = links,
        };
}

/// <summary>A resource object whose attributes are typed as <typeparamref name="TAttributes"/>
/// and whose relationships are a name-keyed dictionary — the escape hatch when the relationship
/// names are not known at compile time. Prefer <see cref="Resource{TAttributes, TRelationships}"/>,
/// which types the relationships as well. <see cref="Resource.Id"/> is null only in a create
/// request where the server assigns ids; <see cref="Attributes"/> is null when the document
/// carried no attributes member (in an update, that means every attribute keeps its current
/// value). A relationship absent from <see cref="Relationships"/> likewise keeps its current
/// value — presence with null data is how a to-one relationship is cleared.</summary>
public sealed record Resource<TAttributes> : Resource where TAttributes : class
{
    [JsonPropertyOrder(1)]
    public TAttributes? Attributes { get; init; }

    [JsonPropertyOrder(2)]
    public IReadOnlyDictionary<string, Relationship>? Relationships { get; init; }

    /// <summary>The named relationship as a to-one, or null when the document did not carry it
    /// (keep the current value). Throws when the document sent an array where a single
    /// identifier belongs.</summary>
    public ToOneRelationship? ToOne(string name)
    {
        if (Relationships is null || !Relationships.TryGetValue(name, out var relationship))
        {
            return null;
        }
        if (relationship is not ToOneRelationship toOne)
        {
            throw new InvalidOperationException(
                $"The '{name}' relationship does not carry to-one resource linkage.");
        }
        return toOne;
    }

    /// <summary>The named relationship as a to-many, or null when the document did not carry it
    /// (keep the current members). Throws when the document sent a single identifier where an
    /// array belongs.</summary>
    public ToManyRelationship? ToMany(string name)
    {
        if (Relationships is null || !Relationships.TryGetValue(name, out var relationship))
        {
            return null;
        }
        if (relationship is not ToManyRelationship toMany)
        {
            throw new InvalidOperationException(
                $"The '{name}' relationship does not carry to-many resource linkage.");
        }
        return toMany;
    }
}

/// <summary>A resource object with both halves typed: attributes as
/// <typeparamref name="TAttributes"/>, relationships as <typeparamref name="TRelationships"/> — a
/// record whose members are <see cref="ToOneRelationship"/>/<see cref="ToManyRelationship"/>,
/// each nullable. A null member is a relationship the document does not carry (keep the current
/// value, omitted when writing); a member set with null data clears a to-one. The declared member
/// type also drives read-side arity checking: an identifier array arriving where a
/// <see cref="ToOneRelationship"/> is declared is rejected as malformed JSON.</summary>
public sealed record Resource<TAttributes, TRelationships> : Resource
    where TAttributes : class
    where TRelationships : class
{
    [JsonPropertyOrder(1)]
    public TAttributes? Attributes { get; init; }

    [JsonPropertyOrder(2)]
    public TRelationships? Relationships { get; init; }
}
