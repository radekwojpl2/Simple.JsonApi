using System.Collections;
using System.Runtime.CompilerServices;

namespace JsonApiLite;

/// <summary>The sideload shape for a document that declares no sideloadable types: the resources
/// as they arrived, untyped. This is the default <c>TIncluded</c> on every resource document form,
/// so it is what <c>Included</c> means when an author declares nothing.</summary>
/// <remarks>It implements <see cref="IReadOnlyList{T}"/> and carries
/// <see cref="CollectionBuilderAttribute"/> deliberately, and that is what keeps this feature's
/// breaking change narrow: indexing, <c>foreach</c>, <c>OfType</c>, assignment to an
/// <c>IReadOnlyList&lt;Resource&gt;</c> and collection-expression literals
/// (<c>Included = [company, tag]</c>) all keep compiling untouched against the previous
/// <c>IReadOnlyList&lt;Resource&gt;?</c> member. Removing either would turn a one-line migration
/// into a rewrite of every call site that reads or assembles a compound document.</remarks>
[CollectionBuilder(typeof(AnyIncludedBuilder), nameof(AnyIncludedBuilder.Create))]
public sealed record AnyIncluded : IIncluded, IReadOnlyList<Resource>
{
    private readonly IReadOnlyList<Resource> _resources;

    /// <summary>Wraps an existing collection without copying it.</summary>
    public AnyIncluded(IReadOnlyList<Resource> resources)
    {
        _resources = resources;
    }

    /// <summary>Every resource in the document, since nothing here is declared: with no members to
    /// resolve against, all of them are undeclared by definition.</summary>
    public IReadOnlyList<Resource> Undeclared => _resources;

    /// <summary>The resource at <paramref name="index"/>, so an existing indexed read keeps
    /// working.</summary>
    public Resource this[int index] => _resources[index];

    /// <summary>How many resources the document sideloaded.</summary>
    public int Count => _resources.Count;

    /// <summary>Enumerates the sideloaded resources in document order.</summary>
    public IEnumerator<Resource> GetEnumerator() => _resources.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Builds an <see cref="AnyIncluded"/> from a collection expression. Referenced only by
/// the <see cref="CollectionBuilderAttribute"/> on <see cref="AnyIncluded"/>; there is no reason to
/// call it directly.</summary>
public static class AnyIncludedBuilder
{
    /// <summary>Materialises the collection expression's elements into an
    /// <see cref="AnyIncluded"/>.</summary>
    public static AnyIncluded Create(ReadOnlySpan<Resource> items) => new(items.ToArray());
}
