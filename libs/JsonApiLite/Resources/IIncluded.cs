namespace JsonApiLite;

/// <summary>Marks a record as a document's sideload shape: one member per resource type the
/// document may sideload, each an <c>IReadOnlyList&lt;Resource&lt;TAttributes, TRelationships&gt;&gt;</c>.
/// Sits over the single flat array the specification requires
/// (https://jsonapi.org/format/#document-compound-documents), exactly as
/// <see cref="IRelationships"/> sits over a name-keyed object — so reaching a sideloaded resource
/// is member access rather than a cast.</summary>
/// <remarks>Empty on purpose, like <see cref="IAttributes"/> and <see cref="IRelationships"/>: it
/// states intent and keeps unrelated types out of the sideload position. The wire type name is
/// never written here as a string either — each member's element type declares it through
/// <see cref="IResourceType"/>, so the name exists once and a typo is a compile error.
/// <para>A resource whose type no member names is dropped when the document is read: a declaration
/// states what the document may carry, and the reader asked for nothing else. A document that
/// cannot enumerate its sideloadable types keeps the untyped <see cref="AnyIncluded"/> instead,
/// which holds every resource that arrives.</para></remarks>
public interface IIncluded;
