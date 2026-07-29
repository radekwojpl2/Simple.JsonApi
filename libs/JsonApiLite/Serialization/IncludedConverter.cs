using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>Converts any <see cref="IIncluded"/> shape, declared or not. Needed even for the
/// undeclared <see cref="AnyIncluded"/>: because that type implements
/// <see cref="IReadOnlyList{T}"/>, System.Text.Json would otherwise claim it with the built-in
/// collection converter and fail on read, having no way to populate a read-only
/// collection.</summary>
internal sealed class IncludedConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(IIncluded).IsAssignableFrom(typeToConvert) && !typeToConvert.IsAbstract;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(IncludedConverter<>).MakeGenericType(typeToConvert))!;
}

/// <summary>Flattens the declared members into the single array the specification requires on
/// write (https://jsonapi.org/format/#document-compound-documents), and buckets that array back
/// into them on read by peeking each element's <c>type</c>. Anything no member claims goes to
/// <see cref="IIncluded.Undeclared"/> rather than being dropped.</summary>
internal sealed class IncludedConverter<TIncluded> : JsonConverter<TIncluded>
    where TIncluded : class, IIncluded
{
    public override TIncluded? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException(
                "A document's 'included' member must be an array of resource objects.");
        }

        var shape = IncludedShape.For(typeof(TIncluded));
        var declared = new Dictionary<string, IList>(StringComparer.Ordinal);

        // AnyIncluded declares nothing and holds everything, so it is the only shape that keeps a
        // resource no member claimed. A declared shape has nowhere to put one and drops it.
        var untyped = typeof(TIncluded) == typeof(AnyIncluded) ? new List<Resource>() : null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var element = JsonElement.ParseValue(ref reader);
            if (Claimed(shape, element, out var member) &&
                element.Deserialize(member.ElementType, options) is Resource resource)
            {
                Bucket(declared, member).Add(resource);
                continue;
            }

            if (untyped is null)
            {
                // Dropped on purpose: a declaration states the resource types the document may
                // carry, so one it does not name was not asked for. The resource is not re-emitted
                // when the document is written back.
                continue;
            }

            // Reads back the way an undeclared document's resources always have — through
            // ResourceConverter, as Resource<JsonObject> or the registered concrete type.
            if (element.Deserialize<Resource>(options) is { } kept)
            {
                untyped.Add(kept);
            }
        }

        return Build(shape, declared, untyped);
    }

    public override void Write(Utf8JsonWriter writer, TIncluded value, JsonSerializerOptions options)
    {
        var shape = IncludedShape.For(typeof(TIncluded));
        writer.WriteStartArray();

        // Declaration order. The specification imposes no ordering within 'included'; this order is
        // fixed only so output stays comparable.
        foreach (var member in shape.Members)
        {
            if (member.Property.GetValue(value) is not IEnumerable resources)
            {
                continue;
            }

            foreach (Resource resource in resources)
            {
                JsonSerializer.Serialize(writer, resource, resource.GetType(), options);
            }
        }

        // AnyIncluded declares no members, so its resources are reached as the collection it is.
        if (value is IEnumerable<Resource> untyped)
        {
            foreach (var resource in untyped)
            {
                JsonSerializer.Serialize(writer, resource, resource.GetType(), options);
            }
        }

        writer.WriteEndArray();
    }

    private static bool Claimed(IncludedShape shape, JsonElement element, out IncludedMember member)
    {
        member = null!;
        if (!element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return shape.TryResolve(type.GetString()!, out member);
    }

    private static IList Bucket(Dictionary<string, IList> declared, IncludedMember member)
    {
        if (declared.TryGetValue(member.ResourceType, out var existing))
        {
            return existing;
        }

        var list = (IList)Activator.CreateInstance(member.ListType)!;
        declared.Add(member.ResourceType, list);
        return list;
    }

    // init-only setters are a compile-time restriction, not a runtime one, so the declared record is
    // built the same way any object initializer would build it.
    private static TIncluded Build(
        IncludedShape shape, Dictionary<string, IList> declared, List<Resource>? untyped)
    {
        if (untyped is not null)
        {
            return (TIncluded)(object)new AnyIncluded(untyped);
        }

        var included = (TIncluded)Activator.CreateInstance(typeof(TIncluded))!;
        foreach (var member in shape.Members)
        {
            if (declared.TryGetValue(member.ResourceType, out var resources))
            {
                member.Property.SetValue(included, resources);
            }
        }

        return included;
    }
}
