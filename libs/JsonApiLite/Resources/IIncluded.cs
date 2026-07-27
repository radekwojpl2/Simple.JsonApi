namespace JsonApiLite;

/// <summary>Marks a record as a document's sideload shape: one member per resource type the
/// document may sideload, each an <c>IReadOnlyList&lt;Resource&lt;TAttributes, TRelationships&gt;&gt;</c>.
/// Sits over the single flat array the specification requires
/// (https://jsonapi.org/format/#document-compound-documents), exactly as
/// <see cref="IRelationships"/> sits over a name-keyed object — so reaching a sideloaded resource
/// is member access rather than a cast.</summary>
/// <remarks>The wire type name is never written here as a string: each member's element type
/// declares it through <see cref="IResourceType"/>, so the name exists once and a typo is a
/// compile error.</remarks>
public interface IIncluded
{
    /// <summary>Sideloaded resources whose type no declared member names. A declared document
    /// offers no untyped view of its sideload member, so this is the only route to a resource the
    /// declaration did not anticipate — without it such a resource would be silently dropped on a
    /// round trip. Empty rather than null when everything resolved.</summary>
    IReadOnlyList<Resource> Undeclared { get; }
}
