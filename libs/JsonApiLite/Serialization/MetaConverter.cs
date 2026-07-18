using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Flattens <see cref="Meta"/> onto the wire: the typed pagination members and the
/// free-form <see cref="Meta.Additional"/> members live in one meta object.</summary>
internal sealed class MetaConverter : JsonConverter<Meta>
{
    public override Meta? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonSerializer.Deserialize<JsonObject>(ref reader, options);
        if (node is null)
        {
            return null;
        }

        int? total = null;
        int? pageCount = null;
        JsonObject? additional = null;
        foreach (var (name, value) in node)
        {
            switch (name)
            {
                case "total" when value is JsonValue totalValue && totalValue.TryGetValue(out int parsedTotal):
                    total = parsedTotal;
                    break;
                case "pageCount" when value is JsonValue pageValue && pageValue.TryGetValue(out int parsedPageCount):
                    pageCount = parsedPageCount;
                    break;
                default:
                    additional ??= [];
                    additional[name] = value?.DeepClone();
                    break;
            }
        }
        return new Meta(total, pageCount) { Additional = additional };
    }

    public override void Write(Utf8JsonWriter writer, Meta value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Total is { } total)
        {
            writer.WriteNumber("total", total);
        }
        if (value.PageCount is { } pageCount)
        {
            writer.WriteNumber("pageCount", pageCount);
        }
        if (value.Additional is not null)
        {
            foreach (var (name, node) in value.Additional)
            {
                writer.WritePropertyName(name);
                if (node is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    node.WriteTo(writer, options);
                }
            }
        }
        writer.WriteEndObject();
    }
}
