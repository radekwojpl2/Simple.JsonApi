namespace JsonApiKit;

/// <summary>Relationship links: self is the relationship endpoint (.../relationships/{name}),
/// related the related-resource endpoint (.../{name}). Either may be null — a links-only to-many
/// with no /relationships/{name} route emits related alone.</summary>
public sealed record RelationshipLinks(string? Self, string? Related = null);
