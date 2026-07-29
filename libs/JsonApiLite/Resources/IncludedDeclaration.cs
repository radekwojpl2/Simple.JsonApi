namespace JsonApiLite;

/// <summary>One resource type a document's sideload shape declares it may carry, and the resource
/// type its entries take on the way in.</summary>
/// <param name="ResourceType">The JSON:API type name claimed on the wire, e.g. "companies".</param>
/// <param name="ElementType">The closed <see cref="Resource{TAttributes, TRelationships}"/> the
/// declaring member holds, from which the attributes and relationships types can be read.</param>
public readonly record struct IncludedResourceType(string ResourceType, Type ElementType);

/// <summary>Reports which resource types an <see cref="IIncluded"/> shape declares, for tooling that
/// has to describe a document rather than read or write one — API descriptions, generated clients,
/// diagnostics.</summary>
/// <remarks>Exists so the declaration an author already made is the single source of that
/// information. Without it, anything wanting to know a document's sideloadable types would have to
/// reflect over the shape a second time, under its own rules, and the two would drift.
/// <para>Deliberately narrow: it reports the wire type name and the element type, and nothing about
/// how the members are filled. That is serialization's business and would become a contract the
/// moment it was published.</para></remarks>
public static class IncludedDeclaration
{
    /// <summary>The resource types <paramref name="includedType"/> declares, in declaration order.
    /// Empty when the shape declares none — including <see cref="AnyIncluded"/>, which declares
    /// nothing and therefore says no more than a document that declares nothing at all.</summary>
    /// <param name="includedType">A closed type implementing <see cref="IIncluded"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="includedType"/> does not implement
    /// <see cref="IIncluded"/>.</exception>
    public static IReadOnlyList<IncludedResourceType> Of(Type includedType)
    {
        ArgumentNullException.ThrowIfNull(includedType);

        if (!typeof(IIncluded).IsAssignableFrom(includedType))
        {
            throw new ArgumentException(
                $"'{includedType}' is not a sideload shape — expected a type implementing IIncluded.",
                nameof(includedType));
        }

        var members = IncludedShape.For(includedType).Members;
        var declared = new List<IncludedResourceType>(members.Count);
        foreach (var member in members)
        {
            declared.Add(new IncludedResourceType(member.ResourceType, member.ElementType));
        }

        return declared;
    }
}
