using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Handles the abstract <see cref="Resource"/> only — closed generic resource types
/// never match, so they keep the default typed handling. Writes the value's concrete resource
/// shape whatever the options, via the attribute on <see cref="Resource"/>. Reads into
/// <c>Resource&lt;JsonObject&gt;</c>, or into the registered concrete type when constructed with
/// a <see cref="ResourceTypeRegistry"/> (<see cref="JsonApiSerializer.CreateOptions"/>).</summary>
internal sealed class ResourceConverter : JsonConverter<Resource>
{
    private readonly ResourceTypeRegistry? _resourceTypes;

    public ResourceConverter()
    {
    }

    internal ResourceConverter(ResourceTypeRegistry resourceTypes)
    {
        _resourceTypes = resourceTypes;
    }

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Resource);

    public override Resource? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (_resourceTypes is null)
        {
            return JsonSerializer.Deserialize<Resource<JsonObject>>(ref reader, options);
        }

        var element = JsonElement.ParseValue(ref reader);
        if (element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
            _resourceTypes.TryResolve(type.GetString()!, out var resourceType))
        {
            return (Resource?)element.Deserialize(resourceType, options);
        }
        return element.Deserialize<Resource<JsonObject>>(options);
    }

    public override void Write(Utf8JsonWriter writer, Resource value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
