using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A relationship object that carries no resource linkage — the spec requires only one of
/// 'links', 'data', or 'meta', so this covers both the links-only form servers emit when the
/// linkage is not included and the rarer meta-only one (<see cref="Relationship.Links"/> is then
/// null). Appears when reading through the dictionary flavor; a typed member declared as
/// <see cref="ToOneRelationship"/> or <see cref="ToManyRelationship"/> still requires 'data',
/// because such a declaration exists to read linkage (and request documents must carry it).</summary>
[JsonConverter(typeof(RelationshipConverter))]
public sealed record LinksRelationship : Relationship;
