using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace JsonApiLite.OpenApi.Tests;

/// <summary>Builds the schema a document type produces and hands it back as JSON. Assertions run
/// against the emitted JSON rather than the OpenApiSchema object graph, because the JSON is what a
/// consumer reads — an object-graph assertion can pass while the serialized document says something
/// else.</summary>
internal static class SchemaFixture
{
    /// <summary>The response schema for <typeparamref name="TDocument"/>, as JSON.</summary>
    public static JsonObject Response<TDocument>(int statusCode = 200) =>
        Json(new JsonApiSchemaBuilder(JsonApiSerializer.Options)
            .Document(JsonApiBody.Response(typeof(TDocument), statusCode)));

    /// <summary>The request body schema for <typeparamref name="TDocument"/>, as JSON.</summary>
    public static JsonObject Request<TDocument>(bool includeId = false) =>
        Json(new JsonApiSchemaBuilder(JsonApiSerializer.Options)
            .Document(JsonApiBody.Request(typeof(TDocument), includeId)));

    /// <summary>Serializes as 3.1 because that is the version the sample's document is written as,
    /// and the schema keywords this package emits — <c>const</c>, type arrays for nullability — are
    /// 3.1 spellings.</summary>
    private static JsonObject Json(IOpenApiSchema schema)
    {
        var text = new StringWriter();
        var writer = new OpenApiJsonWriter(text);
        schema.SerializeAsV31(writer);
        text.Flush();

        return JsonNode.Parse(text.ToString())?.AsObject()
            ?? throw new InvalidOperationException("The schema did not serialize to a JSON object.");
    }

    /// <summary>The named member of an object schema's <c>properties</c>, or null when absent.
    /// Absence and presence are different outcomes throughout this feature, so tests need to tell
    /// them apart rather than fault.</summary>
    public static JsonObject? Property(this JsonObject schema, string name) =>
        schema["properties"]?[name]?.AsObject();

    /// <summary>The schema's declared types. A single string for a member that cannot be null, and an
    /// array when it can — a nullable member is emitted as <c>["null","string"]</c> rather than
    /// <c>"string"</c>, so a test that reads <c>type</c> as a string would fault on exactly the
    /// members most worth checking.</summary>
    public static IReadOnlyCollection<string> TypeNames(this JsonObject? schema)
    {
        var type = schema?["type"];
        if (type is JsonArray array)
        {
            return [.. array.Select(item => item!.GetValue<string>())];
        }

        if (type is null)
        {
            return [];
        }

        return [type.GetValue<string>()];
    }

    /// <summary>The member names an object schema declares, in no particular order.</summary>
    public static IReadOnlyCollection<string> PropertyNames(this JsonObject schema)
    {
        if (schema["properties"] is not JsonObject properties)
        {
            return [];
        }

        return [.. properties.Select(pair => pair.Key)];
    }

    /// <summary>What the schema marks required, empty when it marks nothing.</summary>
    public static IReadOnlyCollection<string> RequiredNames(this JsonObject schema)
    {
        if (schema["required"] is not JsonArray required)
        {
            return [];
        }

        return [.. required.Select(item => item!.GetValue<string>())];
    }

    /// <summary>Every <c>required</c> array anywhere in the document, so a test can assert about the
    /// whole tree rather than one level of it.</summary>
    public static IEnumerable<IReadOnlyCollection<string>> AllRequired(this JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj["required"] is JsonArray required)
            {
                yield return [.. required.Select(item => item!.GetValue<string>())];
            }

            foreach (var child in obj)
            {
                foreach (var found in child.Value.AllRequired())
                {
                    yield return found;
                }
            }

            yield break;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                foreach (var found in item.AllRequired())
                {
                    yield return found;
                }
            }
        }
    }

    /// <summary>Whether a member name appears anywhere in the emitted document. Used to prove a
    /// member is absent everywhere, not merely at the level a test happened to look.</summary>
    public static bool MentionsMember(this JsonNode? node, string name)
    {
        if (node is JsonObject obj)
        {
            if (obj["properties"] is JsonObject properties && properties.ContainsKey(name))
            {
                return true;
            }

            return obj.Any(child => child.Value.MentionsMember(name));
        }

        if (node is JsonArray array)
        {
            return array.Any(item => item.MentionsMember(name));
        }

        return false;
    }
}
