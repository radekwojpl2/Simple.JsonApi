using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JsonApiLite;

/// <summary>The <see cref="JsonSerializerOptions"/> the document types are designed for:
/// camelCase member names, case-insensitive reading, nulls and unset <see cref="Optional{T}"/>
/// members omitted (except where a document type pins 'data' as always-written). Malformed input
/// surfaces as <see cref="JsonException"/>; the caller decides what status that draws.</summary>
public static class JsonApiSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Fresh options, optionally with a <see cref="ResourceTypeRegistry"/>: included
    /// resources whose type name is mapped then deserialize into their registered
    /// <c>Resource&lt;TAttributes, TRelationships&gt;</c> instead of <c>Resource&lt;JsonObject&gt;</c>.</summary>
    public static JsonSerializerOptions CreateOptions(ResourceTypeRegistry? resourceTypes = null)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { OmitUnsetOptionalMembers, OmitEmptySideloads },
            },
        };

        // Unconditional, and not an attribute on IIncluded: a converter attribute on an interface
        // does not reach implementing types, and the declared shapes are the caller's own types
        // anyway. Registering it here is also what stops AnyIncluded being claimed by the built-in
        // collection converter, which cannot populate a read-only collection.
        options.Converters.Add(new IncludedConverterFactory());
        if (resourceTypes is not null)
        {
            options.Converters.Add(new ResourceConverter(resourceTypes));
        }
        return options;
    }

    public static string Serialize<TDocument>(TDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static string Serialize<TDocument>(TDocument document, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(document, options);

    public static TDocument? Deserialize<TDocument>(string json) =>
        JsonSerializer.Deserialize<TDocument>(json, Options);

    public static TDocument? Deserialize<TDocument>(string json, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<TDocument>(json, options);

    /// <summary>A document that sideloads nothing omits 'included' rather than writing it empty.
    /// The null check the other members rely on is not enough here: a declared shape with every
    /// member unset is an object, not a null, and would otherwise write an empty array.</summary>
    private static void OmitEmptySideloads(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (typeof(IIncluded).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = (_, value) =>
                    value is IIncluded included && !IncludedShape.For(included.GetType()).IsEmpty(included);
            }
        }
    }

    /// <summary>An unset <see cref="Optional{T}"/> member is "not in the document" — never write it.</summary>
    private static void OmitUnsetOptionalMembers(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(Optional<>))
            {
                property.ShouldSerialize = (_, value) => value is IOptional { IsSet: true };
            }
        }
    }
}
