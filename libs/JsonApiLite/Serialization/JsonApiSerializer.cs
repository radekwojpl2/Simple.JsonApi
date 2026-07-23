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
                Modifiers = { OmitUnsetOptionalMembers },
            },
        };
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
