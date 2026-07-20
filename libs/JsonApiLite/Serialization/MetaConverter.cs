using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Meta is written exactly as its members stand — the spec names none of them, so there
/// is nothing to reorder or reserve.</summary>
internal sealed class MetaConverter : JsonConverter<Meta>
{
    public override Meta? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var members = JsonSerializer.Deserialize<JsonObject>(ref reader, options);
        if (members is null)
        {
            return null;
        }
        return new Meta { Members = members };
    }

    public override void Write(Utf8JsonWriter writer, Meta value, JsonSerializerOptions options) =>
        value.Members.WriteTo(writer, options);
}
