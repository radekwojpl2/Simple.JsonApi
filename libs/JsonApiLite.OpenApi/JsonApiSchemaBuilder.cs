using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

namespace JsonApiLite.OpenApi;

/// <summary>Builds a JSON:API document schema — the <c>data</c> envelope around a resource whose
/// attributes and relationships are reflected from their CLR types. Member naming and enum
/// representation are taken from the serializer options the app writes with, so the schema and the
/// wire cannot disagree.</summary>
internal sealed class JsonApiSchemaBuilder(JsonSerializerOptions serializerOptions)
{
    public OpenApiSchema Document(JsonApiBody body)
    {
        if (body.Shape == JsonApiShape.Errors)
        {
            return ErrorDocument(body);
        }

        // Linkage documents put resource identifiers under data; resource documents put a full
        // resource object there. Either way a collection wraps that in an array.
        IOpenApiSchema data = body.Shape == JsonApiShape.Linkage
            ? Identifier(nullable: !body.Collection)
            : Resource(body.ResourceType!, body.Attributes!, body.Relationships, body.IncludeId);
        if (body.Collection)
        {
            data = new OpenApiSchema { Type = JsonSchemaType.Array, Items = data };
        }

        var properties = new Dictionary<string, IOpenApiSchema> { ["data"] = data };
        Envelope(body, properties);

        // Only data is required. Every envelope member is optional on the wire, so requiring one
        // would make a response the library itself produces fail validation.
        return Object(properties, ["data"]);
    }

    /// <summary>Adds the envelope members — <c>links</c>, <c>meta</c> and, where the primary data is
    /// resources, <c>included</c>.</summary>
    /// <remarks>Responses only. A request body is the caller's document: it has no links to follow,
    /// nothing sideloaded, and no server-computed metadata, so describing those members would tell a
    /// client to send what it must not — even when the document type carries a declared shape for
    /// them.</remarks>
    private void Envelope(JsonApiBody body, Dictionary<string, IOpenApiSchema> properties)
    {
        if (body is not JsonApiResponseBody)
        {
            return;
        }

        properties["links"] = Links(body.Shape, body.Collection);
        properties["meta"] = Meta(body.Meta);

        // The sideload member holds "resource objects that are related to the primary data", so it
        // does not apply to a document whose primary data is linkage or which has none at all.
        if (body.Shape == JsonApiShape.Resource)
        {
            properties["included"] = Included(body.Included);
        }
    }

    /// <summary>The declared metadata shape walked into named, typed members, or an unconstrained
    /// object when the document declared none.</summary>
    /// <remarks>Unconstrained rather than omitted: the specification reserves no meta member names, so
    /// a document that declares no shape may still carry any members, and omitting the member would
    /// tell a caller it does not exist.</remarks>
    private OpenApiSchema Meta(Type? declared)
    {
        if (declared is null)
        {
            return new OpenApiSchema { Type = JsonSchemaType.Object };
        }

        return Schema(declared, []);
    }

    /// <summary>The sideload member: one flat array, as the specification requires, whose entries are
    /// constrained to the declared resource types when the document named any.</summary>
    private OpenApiSchema Included(Type? declared)
    {
        var entries = new List<IOpenApiSchema>();
        if (declared is not null)
        {
            foreach (var (resourceType, elementType) in IncludedDeclaration.Of(declared))
            {
                entries.Add(SideloadedResource(resourceType, elementType));
            }
        }

        // A declaration naming nothing carries no more information than declaring nothing at all, so
        // both describe an unconstrained resource object rather than a list that can hold nothing.
        if (entries.Count == 0)
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.Object },
            };
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { AnyOf = entries },
        };
    }

    /// <summary>One declared sideloadable type, described as the full resource object it is — the
    /// declared element type is <c>Resource&lt;TAttributes, TRelationships&gt;</c>, so its two type
    /// arguments are exactly what the resource builder already needs.</summary>
    /// <remarks>Always with an id: only a resource originating at the client may omit one, and nothing
    /// arriving in a response's sideload member did.</remarks>
    private OpenApiSchema SideloadedResource(string resourceType, Type elementType)
    {
        var arguments = elementType.GetGenericArguments();
        Type? relationships = null;
        if (arguments.Length >= 2)
        {
            relationships = arguments[1];
        }

        return Resource(resourceType, arguments[0], relationships, includeId: true);
    }

    /// <summary>A resource object, described from its resource type name and the types of its two
    /// halves. Takes those facts rather than a document, because a sideloaded resource needs the same
    /// description and is not a document.</summary>
    private OpenApiSchema Resource(
        string resourceType, Type attributesType, Type? relationshipsType, bool includeId)
    {
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            // A resource object always names its type; here it can only be this one value.
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Const = resourceType },
        };
        var required = new List<string> { "type" };

        if (includeId)
        {
            properties["id"] = new OpenApiSchema { Type = JsonSchemaType.String };
            required.Add("id");
        }

        var attributes = Attributes(attributesType);
        if (attributes.Properties?.Count > 0)
        {
            properties["attributes"] = attributes;
        }

        if (relationshipsType is not null)
        {
            var relationships = Relationships(relationshipsType);
            if (relationships.Properties?.Count > 0)
            {
                properties["relationships"] = relationships;
            }
        }

        return Object(properties, required);
    }

    private OpenApiSchema Attributes(Type attributesType)
    {
        var nullability = new NullabilityInfoContext();
        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (var property in Readable(attributesType))
        {
            var schema = Schema(property.PropertyType, []);
            if (AllowsNull(property, nullability))
            {
                schema.Type |= JsonSchemaType.Null;
            }

            properties[JsonName(property)] = schema;
        }

        return Object(properties, []);
    }

    private OpenApiSchema Relationships(Type relationshipsType)
    {
        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (var property in Readable(relationshipsType))
        {
            var type = property.PropertyType;
            if (type == typeof(ToOneRelationship))
            {
                properties[JsonName(property)] = ToOne();
            }
            else if (type == typeof(ToManyRelationship))
            {
                properties[JsonName(property)] = ToMany();
            }
            else if (type == typeof(Relationship))
            {
                // Declared as the base type, so the document decides: 'data' an array or not.
                properties[JsonName(property)] = new OpenApiSchema { AnyOf = [ToOne(), ToMany()] };
            }
        }

        return Object(properties, []);
    }

    // { "data": { "type": ..., "id": ... } } — null data is the spec's "belongs to nothing".
    private static OpenApiSchema ToOne() =>
        Object(new() { ["data"] = Identifier(nullable: true) }, []);

    // { "data": [ { "type": ..., "id": ... } ] }
    private static OpenApiSchema ToMany() =>
        Object(
            new() { ["data"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = Identifier(nullable: false) } },
            []);

    private static OpenApiSchema Identifier(bool nullable)
    {
        var type = nullable ? JsonSchemaType.Object | JsonSchemaType.Null : JsonSchemaType.Object;
        return new OpenApiSchema
        {
            Type = type,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            Required = new HashSet<string> { "type", "id" },
        };
    }

    /// <summary>The document link members for one document kind.</summary>
    /// <remarks>Written out rather than reflected, for the same reason the error document is: the
    /// <c>Links</c> and <c>Link</c> types carry converters of their own, so walking their CLR shape
    /// would describe fields instead of the wire.
    /// <para>The set varies by kind rather than being uniform. Pagination appears only where the
    /// primary data is a collection ("Pagination links MUST appear in the links object that
    /// corresponds to a collection"), and <c>related</c> only on linkage ("related: a related resource
    /// link when primary data represents a relationship").</para>
    /// <para>There is deliberately no <c>describedby</c>. The specification defines it for a document,
    /// but the library's links object has no such member, so no endpoint built on this can send one —
    /// and describing a member that can never appear is a claim nothing would falsify, because every
    /// link member is optional.</para></remarks>
    private static OpenApiSchema Links(JsonApiShape shape, bool collection)
    {
        var properties = new Dictionary<string, IOpenApiSchema> { ["self"] = Link() };

        if (shape == JsonApiShape.Linkage)
        {
            properties["related"] = Link();
        }

        if (collection)
        {
            properties["first"] = Link();
            properties["prev"] = Link();
            properties["next"] = Link();
            properties["last"] = Link();
        }

        return Object(properties, []);
    }

    // A bare URI when the link carries nothing else, a link object when it carries meta.
    private static OpenApiSchema Link() =>
        new()
        {
            AnyOf =
            [
                new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
                Object(
                    new()
                    {
                        ["href"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
                        ["meta"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    },
                    ["href"]),
            ],
        };

    // Written out rather than reflected: Links and Meta carry converters of their own, so walking
    // the CLR shape of Error would describe the fields instead of the wire.
    private OpenApiSchema ErrorDocument(JsonApiBody body)
    {
        var source = Object(
            new()
            {
                ["pointer"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["parameter"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            []);

        var error = Object(
            new()
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["source"] = source,
                ["meta"] = new OpenApiSchema { Type = JsonSchemaType.Object },
            },
            []);

        // Through the same seam as every other kind, so an error response's envelope cannot drift
        // from a data response's. Its metadata is unconstrained by necessity rather than by choice:
        // ErrorDocument is non-generic, so an author has no way to declare a shape for it.
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            ["errors"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = error },
        };
        Envelope(body, properties);

        return Object(properties, ["errors"]);
    }

    /// <summary>An attribute value's schema. Unwraps the carriers that only mean "may be absent",
    /// then describes what is left — scalar, enum, dictionary, sequence, or a nested object walked
    /// the same way. <paramref name="seen"/> breaks self-referencing types.</summary>
    private OpenApiSchema Schema(Type type, HashSet<Type> seen)
    {
        type = Unwrap(type);

        if (Scalar(type) is { } scalar)
        {
            return scalar;
        }

        if (type.IsEnum)
        {
            return Enumeration(type);
        }

        // Checked before sequences: a dictionary is also an IEnumerable, of pairs.
        if (DictionaryValueOf(type) is { } valueType)
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = Schema(valueType, seen),
            };
        }

        if (ElementOf(type) is { } elementType)
        {
            return new OpenApiSchema { Type = JsonSchemaType.Array, Items = Schema(elementType, seen) };
        }

        // A nested object already on the path would recurse forever; describe it as a bare object.
        if (!seen.Add(type))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Object };
        }

        var nullability = new NullabilityInfoContext();
        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (var property in Readable(type))
        {
            var schema = Schema(property.PropertyType, seen);
            if (AllowsNull(property, nullability))
            {
                schema.Type |= JsonSchemaType.Null;
            }

            properties[JsonName(property)] = schema;
        }

        seen.Remove(type);
        return Object(properties, []);
    }

    private static OpenApiSchema Object(Dictionary<string, IOpenApiSchema> properties, IReadOnlyCollection<string> required)
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties };
        if (required.Count > 0)
        {
            schema.Required = required.ToHashSet();
        }

        return schema;
    }

    private static OpenApiSchema? Scalar(Type type)
    {
        if (type == typeof(string) || type == typeof(char))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String };
        }
        if (type == typeof(bool))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Boolean };
        }
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer };
        }
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Number };
        }
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };
        }
        if (type == typeof(DateOnly))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };
        }
        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "time" };
        }
        if (type == typeof(Guid))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
        }
        if (type == typeof(Uri))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" };
        }
        // Not a sequence on the wire: System.Text.Json writes bytes as base64.
        if (type == typeof(byte[]))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
        }

        return null;
    }

    /// <summary>Enum members as they are actually written: serializing each one answers both whether
    /// this app writes enums as strings or numbers, and what a string member is named — neither is
    /// recoverable from the CLR type, since both depend on the converters in play.</summary>
    private OpenApiSchema Enumeration(Type enumType)
    {
        var values = new List<JsonNode>();
        foreach (var member in Enum.GetValues(enumType))
        {
            if (JsonNode.Parse(JsonSerializer.Serialize(member, enumType, serializerOptions)) is { } node)
            {
                values.Add(node);
            }
        }

        var written = values.Count > 0 && values[0].GetValueKind() == JsonValueKind.String;
        return new OpenApiSchema
        {
            Type = written ? JsonSchemaType.String : JsonSchemaType.Integer,
            Enum = values,
        };
    }

    // Unwrap Optional<T> (the PATCH tri-state carrier) and Nullable<T> down to the payload type.
    private static Type Unwrap(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>))
        {
            type = type.GetGenericArguments()[0];
        }

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    /// <summary>Whether the member may be written as null. Optional&lt;T&gt; counts: for a PATCH an
    /// explicit null is the whole point — it means "clear this", as against omitting the member.</summary>
    private static bool AllowsNull(PropertyInfo property, NullabilityInfoContext nullability)
    {
        var type = property.PropertyType;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>))
        {
            return true;
        }
        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return true;
        }

        return nullability.Create(property).ReadState == NullabilityState.Nullable;
    }

    private static Type? DictionaryValueOf(Type type)
    {
        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            var definition = candidate.GetGenericTypeDefinition();
            var isDictionary = definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>);
            if (isDictionary && candidate.GetGenericArguments()[0] == typeof(string))
            {
                return candidate.GetGenericArguments()[1];
            }
        }

        return null;
    }

    private static Type? ElementOf(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static IEnumerable<PropertyInfo> Readable(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && !Ignored(property));

    // Only an unconditional [JsonIgnore] takes the member off the wire; the other conditions —
    // WhenWritingNull and friends — still leave it in the document sometimes.
    private static bool Ignored(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always };

    private string JsonName(PropertyInfo property)
    {
        var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (name is not null)
        {
            return name;
        }

        return serializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
    }
}
