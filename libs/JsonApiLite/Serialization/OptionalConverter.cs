using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonApiLite;

internal sealed class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionalConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
}

/// <summary>A present member always reads as set — including an explicit null, which is the
/// whole point of <see cref="Optional{T}"/>. Unset values are never written under
/// <see cref="JsonApiSerializer"/> options, whose contract modifier skips them.</summary>
internal sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
{
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) =>
        Optional<T>.Of(JsonSerializer.Deserialize<T>(ref reader, options));

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Value, options);
}
