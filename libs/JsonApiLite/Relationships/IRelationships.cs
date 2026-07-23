namespace JsonApiLite;

/// <summary>Marks a record as a resource's relationships shape — members typed as
/// <see cref="ToOneRelationship"/> or <see cref="ToManyRelationship"/>, each nullable. Empty on
/// purpose, like <see cref="IAttributes"/>: it states intent and keeps unrelated types out of the
/// relationships position.</summary>
public interface IRelationships;
