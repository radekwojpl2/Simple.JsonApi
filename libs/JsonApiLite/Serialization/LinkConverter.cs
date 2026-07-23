using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Reads a link from either wire form — bare URI string or link object — and writes the
/// smallest form that carries the value: a string unless meta forces a link object.</summary>
internal sealed class LinkConverter : JsonConverter<Link>
{
    public override Link Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new Link(reader.GetString()!);
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A link must be a URI string or a link object with an 'href' member.");
        }

        string? href = null;
        Meta? meta = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "href":
                    href = reader.GetString();
                    break;
                case "meta":
                    meta = JsonSerializer.Deserialize<Meta>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        if (href is null)
        {
            throw new JsonException("A link object must contain a string 'href' member.");
        }
        return new Link(href) { Meta = meta };
    }

    public override void Write(Utf8JsonWriter writer, Link value, JsonSerializerOptions options)
    {
        if (value.Meta is null)
        {
            writer.WriteStringValue(value.Href);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("href", value.Href);
        writer.WritePropertyName("meta");
        JsonSerializer.Serialize(writer, value.Meta, options);
        writer.WriteEndObject();
    }
}
