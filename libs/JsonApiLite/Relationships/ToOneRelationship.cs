using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A to-one relationship: <see cref="Data"/> is the target, or null for an empty
/// relationship (in a request, that means "clear it"). Absence is modelled one level up: the name
/// missing from a relationships dictionary, or a null member on a typed relationships record.</summary>
[JsonConverter(typeof(RelationshipConverter))]
public sealed record ToOneRelationship(ResourceIdentifier? Data) : Relationship;
