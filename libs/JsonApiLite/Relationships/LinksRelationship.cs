using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>A relationship object that carries links but no resource linkage — the spec requires
/// only one of 'links', 'data', or 'meta', and servers emit this form when the linkage itself is
/// not included. Appears when reading through the dictionary flavor; a typed member declared as
/// <see cref="ToOneRelationship"/> or <see cref="ToManyRelationship"/> still requires 'data',
/// because such a declaration exists to read linkage (and request documents must carry it).</summary>
[JsonConverter(typeof(RelationshipConverter))]
public sealed record LinksRelationship : Relationship;
