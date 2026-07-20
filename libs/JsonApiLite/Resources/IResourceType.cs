namespace JsonApiLite;

/// <summary>Implemented by an attributes record to declare, in one place, the JSON:API resource
/// type name its resource serializes as. Everywhere else the name is needed — relationship
/// linkage, resource construction, the type registry — it is pulled from the type parameter, so
/// the string exists exactly once and a typo is a compile error. The name is declared rather
/// than derived, because spec type names ("contacts") are not mechanically recoverable from CLR
/// type names (ContactAttributes).</summary>
public interface IResourceType : IAttributes
{
    /// <summary>The JSON:API resource type name, e.g. "contacts".</summary>
    static abstract string ResourceType { get; }
}
