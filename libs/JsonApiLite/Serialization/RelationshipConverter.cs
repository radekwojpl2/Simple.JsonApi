using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Reads and writes <see cref="Relationship"/> objects. Reading decides the arity from
/// whether 'data' is an array — unless the declared type already names it, in which case a
/// mismatch is rejected as malformed JSON. Writing always emits 'data', because an explicit
/// data: null (empty to-one) must never be dropped by null-omission.</summary>
internal sealed class RelationshipConverter : JsonConverter<Relationship>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(Relationship).IsAssignableFrom(typeToConvert);

    public override Relationship Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A relationship must be an object with a 'data' member.");
        }

        var hasData = false;
        ResourceIdentifier? one = null;
        List<ResourceIdentifier>? many = null;
        Links? links = null;
        Meta? meta = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "data":
                    hasData = true;
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        many = JsonSerializer.Deserialize<List<ResourceIdentifier>>(ref reader, options);
                    }
                    else if (reader.TokenType != JsonTokenType.Null)
                    {
                        one = JsonSerializer.Deserialize<ResourceIdentifier>(ref reader, options);
                    }
                    break;
                case "links":
                    links = JsonSerializer.Deserialize<Links>(ref reader, options);
                    break;
                case "meta":
                    meta = JsonSerializer.Deserialize<Meta>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (!hasData)
        {
            if (typeToConvert == typeof(ToOneRelationship) || typeToConvert == typeof(ToManyRelationship))
            {
                throw new JsonException(
                    "A relationship declared to carry resource linkage must contain a 'data' member.");
            }
            if (links is null && meta is null)
            {
                throw new JsonException(
                    "A relationship object must contain a 'data', 'links' or 'meta' member.");
            }
            return new LinksRelationship { Links = links, Meta = meta };
        }
        if (many is not null)
        {
            if (typeToConvert == typeof(ToOneRelationship))
            {
                throw new JsonException(
                    "The relationship's 'data' is an identifier array, but a to-one relationship " +
                    "(a single identifier or null) is declared here.");
            }
            return new ToManyRelationship(many) { Links = links, Meta = meta };
        }
        if (typeToConvert == typeof(ToManyRelationship))
        {
            throw new JsonException(
                "The relationship's 'data' is a single identifier or null, but a to-many " +
                "relationship (an identifier array) is declared here.");
        }
        return new ToOneRelationship(one) { Links = links, Meta = meta };
    }

    public override void Write(Utf8JsonWriter writer, Relationship value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Links is not null)
        {
            writer.WritePropertyName("links");
            JsonSerializer.Serialize(writer, value.Links, options);
        }
        if (value is not LinksRelationship)
        {
            writer.WritePropertyName("data");
            if (value is ToManyRelationship toMany)
            {
                JsonSerializer.Serialize(writer, toMany.Data, options);
            }
            else if (((ToOneRelationship)value).Data is { } identifier)
            {
                JsonSerializer.Serialize(writer, identifier, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
        if (value.Meta is not null)
        {
            writer.WritePropertyName("meta");
            JsonSerializer.Serialize(writer, value.Meta, options);
        }
        writer.WriteEndObject();
    }
}
