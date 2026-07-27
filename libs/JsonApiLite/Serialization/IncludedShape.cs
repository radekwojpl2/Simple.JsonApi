using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace JsonApiLite;

/// <summary>One declared member of an <see cref="IIncluded"/> shape: the wire type name it claims,
/// the property holding it, and the concrete resource type its elements deserialize into.</summary>
internal sealed class IncludedMember
{
    internal IncludedMember(string resourceType, PropertyInfo property, Type elementType)
    {
        ResourceType = resourceType;
        Property = property;
        ElementType = elementType;
        ListType = typeof(List<>).MakeGenericType(elementType);
    }

    public string ResourceType { get; }
    public PropertyInfo Property { get; }
    public Type ElementType { get; }
    public Type ListType { get; }
}

/// <summary>The map from a JSON:API resource type name to the declared member that holds it, built
/// once per closed <see cref="IIncluded"/> type by reflecting over its members.</summary>
/// <remarks>Cached because reflection here is per <em>type</em>, not per document: a converter that
/// re-reflected on every read would put the cost of the declaration on every request. This is also
/// why a source generator was not needed — the work happens once.</remarks>
internal sealed class IncludedShape
{
    private static readonly ConcurrentDictionary<Type, IncludedShape> Cache = new();

    private readonly Dictionary<string, IncludedMember> _byResourceType;

    public static IncludedShape For(Type includedType) =>
        Cache.GetOrAdd(includedType, static type => new IncludedShape(type));

    private IncludedShape(Type includedType)
    {
        Type = includedType;

        // Declaration order is fixed here, at construction, so the write path is deterministic. The
        // specification imposes no ordering within 'included', so the order itself is a free choice;
        // what is not free is stability, because existing tests compare serialized output byte for
        // byte (SerializationTests.cs:108, CompoundDocumentTests.cs:11). Ordered by metadata token
        // rather than taken as GetProperties returns them, because that method's documentation is
        // explicit that it "does not return properties in a particular order".
        var members = new List<IncludedMember>();
        var byResourceType = new Dictionary<string, IncludedMember>(StringComparer.Ordinal);
        var properties = includedType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(properties, static (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
        foreach (var property in properties)
        {
            if (DeclaredElementOf(property) is not { } elementType)
            {
                continue;
            }

            var resourceType = ResourceTypeNameOf(elementType);
            var member = new IncludedMember(resourceType, property, elementType);
            if (byResourceType.TryGetValue(resourceType, out var existing))
            {
                throw new InvalidOperationException(
                    $"'{includedType.Name}' declares the resource type '{resourceType}' twice — on " +
                    $"'{existing.Property.Name}' and '{property.Name}'. A wire type name resolves to " +
                    "exactly one member, so the document could not be read back unambiguously.");
            }

            byResourceType.Add(resourceType, member);
            members.Add(member);
        }

        _byResourceType = byResourceType;
        Members = members;
    }

    public Type Type { get; }

    /// <summary>The declared members, in the order they appear on the type.</summary>
    public IReadOnlyList<IncludedMember> Members { get; }

    public bool TryResolve(string resourceType, out IncludedMember member) =>
        _byResourceType.TryGetValue(resourceType, out member!);

    /// <summary>Whether the shape holds no resources at all. FR-011 omits the sideload member in
    /// that case rather than writing an empty array, which the default null check cannot do — a
    /// declared record with every member unset is not itself null.</summary>
    public bool IsEmpty(IIncluded value)
    {
        foreach (var member in Members)
        {
            if (member.Property.GetValue(value) is ICollection { Count: > 0 })
            {
                return false;
            }
        }

        // AnyIncluded declares no members and is itself the collection, so the loop above can never
        // see its contents.
        if (value is IReadOnlyCollection<Resource> { Count: > 0 })
        {
            return false;
        }

        return true;
    }

    /// <summary>The concrete resource type a member holds, or null when the member is not a
    /// declaration — an indexer or a count is not a closed generic resource list and so never
    /// matches.</summary>
    private static Type? DeclaredElementOf(PropertyInfo property)
    {
        if (!property.CanRead || !property.PropertyType.IsGenericType ||
            property.PropertyType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>))
        {
            return null;
        }

        var element = property.PropertyType.GetGenericArguments()[0];
        if (!element.IsGenericType || !typeof(Resource).IsAssignableFrom(element))
        {
            return null;
        }

        return element;
    }

    // Invoked through a generic method rather than GetProperty, because IResourceType.ResourceType
    // is a static abstract: an implementing type may satisfy it explicitly, and an explicit
    // implementation is not a public static property that reflection would find by name.
    private static readonly MethodInfo NameAccessor =
        typeof(IncludedShape).GetMethod(nameof(NameOf), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string NameOf<TAttributes>() where TAttributes : IResourceType =>
        TAttributes.ResourceType;

    private static string ResourceTypeNameOf(Type resourceType)
    {
        var attributes = resourceType.GetGenericArguments()[0];
        if (!typeof(IResourceType).IsAssignableFrom(attributes))
        {
            throw new InvalidOperationException(
                $"'{attributes.Name}' must implement {nameof(IResourceType)} to be declared as a " +
                "sideloadable type, so its resource type name is stated exactly once.");
        }

        return (string)NameAccessor.MakeGenericMethod(attributes).Invoke(null, null)!;
    }
}
