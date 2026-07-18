using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A to-many relationship: <see cref="Data"/> is the complete member set, possibly
/// empty (in a request, an empty array means "remove every member").</summary>
[JsonConverter(typeof(RelationshipConverter))]
public sealed record ToManyRelationship(IReadOnlyList<ResourceIdentifier> Data) : Relationship;
