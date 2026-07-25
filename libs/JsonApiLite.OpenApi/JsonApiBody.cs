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
/// the transformer to read. Built from a document type — its shape, the attribute and relationship
/// types, the resource name, and whether it is a collection are all read off it. Requests and
/// responses are separate subtypes so a request cannot carry a status code it has no use for.</summary>
internal abstract class JsonApiBody
{
    private protected JsonApiBody(Description description)
    {
        Shape = description.Shape;
        ResourceType = description.ResourceType;
        Attributes = description.Attributes;
        Relationships = description.Relationships;
        Collection = description.Collection;
    }

    public JsonApiShape Shape { get; }
    public string? ResourceType { get; }
    public Type? Attributes { get; }
    public Type? Relationships { get; }
    public bool Collection { get; }
    public bool IncludeId { get; init; }

    public static JsonApiBody Request(Type documentType, bool includeId) =>
        new JsonApiRequestBody(Describe(documentType)) { IncludeId = includeId };

    public static JsonApiBody Response(Type documentType, int statusCode) =>
        new JsonApiResponseBody(Describe(documentType)) { IncludeId = true, StatusCode = statusCode };

    // Matched against the open generic types themselves rather than by type name, so a rename is a
    // compile error here instead of a document that silently loses its schemas.
    private static readonly HashSet<Type> SingleDocuments =
    [
        typeof(ResourceDocument<>),
        typeof(ResourceDocument<,>),
        typeof(ResourceDocument<,,>),
    ];

    private static readonly HashSet<Type> CollectionDocuments =
    [
        typeof(ResourceCollectionDocument<>),
        typeof(ResourceCollectionDocument<,>),
        typeof(ResourceCollectionDocument<,,>),
    ];

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

        // ResourceDocument<TAttributes[, TRelationships[, TMeta]]> and its collection twin. The first
        // type argument is always the attributes; the second, when present, is the relationships.
        if (documentType.IsGenericType)
        {
            var definition = documentType.GetGenericTypeDefinition();
            var collection = CollectionDocuments.Contains(definition);
            if (collection || SingleDocuments.Contains(definition))
            {
                var arguments = documentType.GetGenericArguments();
                var attributes = arguments[0];
                return new Description(JsonApiShape.Resource, collection)
                {
                    ResourceType = ResourceTypeOf(attributes),
                    Attributes = attributes,
                    Relationships = arguments.Length >= 2 ? arguments[1] : null,
                };
            }
        }

        throw new ArgumentException(
            $"'{documentType}' is not a JSON:API document the annotation understands — expected " +
            "ResourceDocument<>, ResourceCollectionDocument<>, ToOneLinkageDocument, " +
            "ToManyLinkageDocument, or ErrorDocument.");
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
