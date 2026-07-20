using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A relationship object (https://jsonapi.org/format/#document-resource-object-relationships):
/// either <see cref="ToOneRelationship"/> or <see cref="ToManyRelationship"/>, decided on read by
/// whether 'data' is an array. The 'data' member is always written, because an explicit
/// data: null is how a to-one relationship is emptied — omission means something else entirely
/// (the relationship keeps its current value).</summary>
[JsonConverter(typeof(RelationshipConverter))]
public abstract record Relationship
{
    public Links? Links { get; init; }

    /// <summary>Relationship-level meta, free-form per the spec — a member count, a role, a
    /// timestamp on the association itself rather than on either resource.</summary>
    public Meta? Meta { get; init; }

    public static ToOneRelationship ToOne(string type, string id) =>
        new(new ResourceIdentifier(type, id));

    /// <summary>Same, with the target type name pulled from the attributes type's
    /// <see cref="IResourceType"/> declaration — a mistyped resource type cannot compile.</summary>
    public static ToOneRelationship ToOne<TAttributes>(string id) where TAttributes : IResourceType =>
        new(new ResourceIdentifier(TAttributes.ResourceType, id));

    public static ToOneRelationship EmptyToOne() => new(null);

    public static ToManyRelationship ToMany(string type, IEnumerable<string> ids) =>
        new([.. ids.Select(id => new ResourceIdentifier(type, id))]);

    /// <summary>Same, with the member type name pulled from the attributes type's
    /// <see cref="IResourceType"/> declaration.</summary>
    public static ToManyRelationship ToMany<TAttributes>(IEnumerable<string> ids)
        where TAttributes : IResourceType =>
        new([.. ids.Select(id => new ResourceIdentifier(TAttributes.ResourceType, id))]);
}
