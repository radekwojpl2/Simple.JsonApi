namespace JsonApiLite;

/// <summary>Marks a record as a resource's attributes shape. Empty on purpose: it states intent
/// and keeps unrelated types out of the attributes position, nothing more. Attributes types that
/// declare a resource type name satisfy it through <see cref="IResourceType"/>; implement it
/// directly on those that do not.</summary>
public interface IAttributes;
