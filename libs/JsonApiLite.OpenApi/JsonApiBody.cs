using System.Reflection;

namespace JsonApiLite.OpenApi;

internal enum JsonApiShape
{
    /// <summary>A resource object under <c>data</c> — type, id, attributes, relationships.</summary>
    Resource,

    /// <summary>Linkage only: resource identifiers under <c>data</c>, as the relationship
    /// endpoints send and return.</summary>
    Linkage,

    /// <summary>An error document: <c>errors</c>, never primary data.</summary>
    Errors,
}

/// <summary>One JSON:API document an endpoint accepts or returns, attached as endpoint metadata for
/// the transformer to read. Built from a document type — its shape, the attribute, relationship,
/// metadata and sideload types, the resource name, and whether it is a collection are all read off
/// it. Requests and responses are separate subtypes so a request cannot carry a status code it has
/// no use for, and so the envelope members can be described on responses alone.</summary>
internal abstract class JsonApiBody
{
    private protected JsonApiBody(Description description)
    {
        Shape = description.Shape;
        ResourceType = description.ResourceType;
        Attributes = description.Attributes;
        Relationships = description.Relationships;
        Meta = description.Meta;
        Included = description.Included;
        Collection = description.Collection;
    }

    public JsonApiShape Shape { get; }
    public string? ResourceType { get; }
    public Type? Attributes { get; }
    public Type? Relationships { get; }

    /// <summary>The document's declared metadata shape, or null when the document leaves it untyped.
    /// Null means "describe an object whose members are unconstrained", never "omit the member".</summary>
    public Type? Meta { get; }

    /// <summary>The document's declared sideload shape, or null when it declares no sideloadable
    /// types. Null means "describe an unconstrained list of resources".</summary>
    public Type? Included { get; }

    public bool Collection { get; }
    public bool IncludeId { get; init; }

    public static JsonApiBody Request(Type documentType, bool includeId) =>
        new JsonApiRequestBody(Describe(documentType)) { IncludeId = includeId };

    public static JsonApiBody Response(Type documentType, int statusCode) =>
        new JsonApiResponseBody(Describe(documentType)) { IncludeId = true, StatusCode = statusCode };

    private static Description Describe(Type documentType)
    {
        // Linkage and error documents are non-generic: no attributes, no resource type. To-many
        // linkage is a data array; to-one is a single (nullable) identifier.
        if (documentType == typeof(ToOneLinkageDocument))
        {
            return new Description(JsonApiShape.Linkage, Collection: false);
        }
        if (documentType == typeof(ToManyLinkageDocument))
        {
            return new Description(JsonApiShape.Linkage, Collection: true);
        }
        if (documentType == typeof(ErrorDocument))
        {
            return new Description(JsonApiShape.Errors, Collection: false);
        }

        // Every fully typed document form reaches the four-argument base by inheritance, so the
        // arguments are read off that base.
        if (FullyTypedBase(documentType, typeof(ResourceDocument<,,,>)) is { } single)
        {
            return FullyTyped(single, collection: false);
        }
        if (FullyTypedBase(documentType, typeof(ResourceCollectionDocument<,,,>)) is { } collection)
        {
            return FullyTyped(collection, collection: true);
        }

        // The single-argument forms sit outside that chain: their relationships are a name-keyed
        // dictionary rather than a declared record, so there is nothing for the later arguments to
        // mean and they describe no envelope member beyond links.
        if (documentType.IsGenericType)
        {
            var definition = documentType.GetGenericTypeDefinition();
            if (definition == typeof(ResourceDocument<>))
            {
                return Untyped(documentType, collection: false);
            }
            if (definition == typeof(ResourceCollectionDocument<>))
            {
                return Untyped(documentType, collection: true);
            }
        }

        throw new ArgumentException(
            $"'{documentType}' is not a JSON:API document the annotation understands — expected " +
            "ResourceDocument<>, ResourceCollectionDocument<>, ToOneLinkageDocument, " +
            "ToManyLinkageDocument, or ErrorDocument.");
    }

    /// <summary>The closed four-argument base of a document type, or null when it has none.</summary>
    /// <remarks>Walked rather than matched against one closed arity per form, because the forms are an
    /// inheritance chain and not independent arities: <c>ResourceDocument&lt;A,R&gt;</c> derives from
    /// <c>ResourceDocument&lt;A,R,Meta&gt;</c>, which derives from
    /// <c>ResourceDocument&lt;A,R,Meta,AnyIncluded&gt;</c>. Walking to the root reads every form
    /// through one path with its defaults already substituted, and a convenience form added later
    /// works without being listed here — the enumerated list this replaced silently stopped
    /// understanding a document the moment a fourth form was added. Still matched against the open
    /// generic type itself rather than by name, so a rename is a compile error here.</remarks>
    private static Type? FullyTypedBase(Type documentType, Type definition)
    {
        for (var candidate = documentType; candidate is not null; candidate = candidate.BaseType)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == definition)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Description FullyTyped(Type closedBase, bool collection)
    {
        var arguments = closedBase.GetGenericArguments();
        var attributes = arguments[0];
        return new Description(JsonApiShape.Resource, collection)
        {
            ResourceType = ResourceTypeOf(attributes),
            Attributes = attributes,
            Relationships = arguments[1],
            Meta = DeclaredMeta(arguments[2]),
            Included = DeclaredIncluded(arguments[3]),
        };
    }

    private static Description Untyped(Type documentType, bool collection)
    {
        var attributes = documentType.GetGenericArguments()[0];
        return new Description(JsonApiShape.Resource, collection)
        {
            ResourceType = ResourceTypeOf(attributes),
            Attributes = attributes,
        };
    }

    /// <summary>The metadata shape worth reflecting, or null when there is none to reflect.</summary>
    /// <remarks><see cref="Meta"/> and <c>Meta&lt;T&gt;</c> carry their wire form in a single
    /// <c>JsonObject</c> behind a converter, so walking either would describe a <c>members</c> field
    /// that is never written. Only a shape that does not derive from it is a plain record whose
    /// properties are what goes on the wire. Tested for by derivation rather than equality, because
    /// <c>Meta&lt;T&gt;</c> satisfies the document's type constraint too.</remarks>
    private static Type? DeclaredMeta(Type meta)
    {
        if (typeof(Meta).IsAssignableFrom(meta))
        {
            return null;
        }

        return meta;
    }

    /// <summary>The sideload shape worth reading, or null when the document declares no types.</summary>
    /// <remarks><see cref="AnyIncluded"/> is the default on every form and declares no members, so it
    /// carries no more information than declaring nothing at all — and must be described the same
    /// way, as an unconstrained list.</remarks>
    private static Type? DeclaredIncluded(Type included)
    {
        if (included == typeof(AnyIncluded))
        {
            return null;
        }

        return included;
    }

    // The resource name comes from the attributes type's static IResourceType.ResourceType.
    private static string ResourceTypeOf(Type attributes)
    {
        var property = attributes.GetProperty("ResourceType", BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is string resourceType)
        {
            return resourceType;
        }

        throw new ArgumentException($"'{attributes}' must implement IResourceType to name its resource.");
    }

    /// <summary>What <see cref="Describe"/> reads off a document type, before the request/response
    /// split adds what only one of them has.</summary>
    internal readonly record struct Description(JsonApiShape Shape, bool Collection)
    {
        public string? ResourceType { get; init; }
        public Type? Attributes { get; init; }
        public Type? Relationships { get; init; }
        public Type? Meta { get; init; }
        public Type? Included { get; init; }
    }
}

internal sealed class JsonApiRequestBody : JsonApiBody
{
    internal JsonApiRequestBody(Description description) : base(description)
    {
    }
}

internal sealed class JsonApiResponseBody : JsonApiBody
{
    internal JsonApiResponseBody(Description description) : base(description)
    {
    }

    public required int StatusCode { get; init; }
}
