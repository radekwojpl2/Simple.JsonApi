using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

/// <summary>The primary resource object of a JSON:API create or update request
/// (https://jsonapi.org/format/#crud), parsed and validated against the endpoint: a request that
/// is not a document with a single resource object draws 400, a type (or update id) that does not
/// match the endpoint draws 409, and a client-generated id on a create draws 403. Attributes and
/// to-one linkages are then read through <see cref="TryReadAttributes{T}"/> and
/// <see cref="TryGetToOne"/> for the endpoint to map onto its write model.</summary>
public sealed class ResourceDocument
{
    private readonly JsonObject _attributes;
    private readonly JsonObject _relationships;

    private ResourceDocument(JsonObject attributes, JsonObject relationships)
    {
        _attributes = attributes;
        _relationships = relationships;
    }

    /// <summary>Parses the body of a create request for the <paramref name="endpointType"/>
    /// collection. Returns an error result — 400 for a malformed document, 409 for a type that
    /// does not match the collection, 403 for a client-generated id (this server assigns ids) —
    /// or null on success with <paramref name="document"/> set.</summary>
    public static IResult? TryParseCreate(JsonNode? body, string endpointType, out ResourceDocument document)
    {
        var error = TryParse(body, endpointType, out document, out var id);
        if (error is not null)
        {
            return error;
        }
        if (id is not null)
        {
            return JsonApiResults.Forbidden(
                "Client-generated ids are not supported; omit the 'id' member and the server assigns one.");
        }
        return null;
    }

    /// <summary>Parses the body of an update request addressed to <paramref name="endpointType"/>/
    /// <paramref name="endpointId"/>. Returns an error result — 400 for a malformed document or a
    /// resource object without 'type' and 'id', 409 when either does not match the endpoint — or
    /// null on success with <paramref name="document"/> set.</summary>
    public static IResult? TryParseUpdate(JsonNode? body, string endpointType, string endpointId,
        out ResourceDocument document)
    {
        var error = TryParse(body, endpointType, out document, out var id);
        if (error is not null)
        {
            return error;
        }
        if (id is null)
        {
            return JsonApiResults.BadRequest("Invalid resource document",
                "The resource object of an update request must contain 'type' and 'id' members.");
        }
        if (id != endpointId)
        {
            return JsonApiResults.Conflict(
                $"The resource object's id '{id}' does not match the endpoint's '{endpointId}'.");
        }
        return null;
    }

    /// <summary>Deserializes the attributes object into <typeparamref name="T"/> with web JSON
    /// naming, so an endpoint reads attributes through the same shape it binds flat JSON to.
    /// Attributes absent from the document stay at their default values, which is how the spec's
    /// "missing attributes keep their current values" reaches the partial-update commands.</summary>
    public IResult? TryReadAttributes<T>(out T? attributes)
    {
        try
        {
            attributes = _attributes.Deserialize<T>(JsonApiResults.SerializerOptions);
            return null;
        }
        catch (JsonException)
        {
            attributes = default;
            return JsonApiResults.BadRequest("Invalid resource document",
                "One or more attributes have values of the wrong JSON type.");
        }
    }

    /// <summary>Reads the to-one relationship <paramref name="name"/>. Returns an error result —
    /// 400 when it is not a relationship object with a 'data' member holding null or a resource
    /// identifier, 409 when the identifier's type is not <paramref name="expectedType"/> — or
    /// null on success. A missing relationship sets <paramref name="present"/> false (per the
    /// spec it keeps its current value); data: null sets <paramref name="id"/> null (clear).</summary>
    public IResult? TryGetToOne(string name, string expectedType, out bool present, out string? id)
    {
        id = null;
        present = _relationships.TryGetPropertyValue(name, out var relationship);
        if (!present)
        {
            return null;
        }
        if (relationship is not JsonObject relationshipObject ||
            !relationshipObject.TryGetPropertyValue("data", out var data))
        {
            return JsonApiResults.BadRequest("Invalid resource document",
                $"The '{name}' relationship must be a relationship object with a 'data' member.");
        }
        if (data is null)
        {
            return null;
        }
        if (data is not JsonObject identifier ||
            identifier["type"] is not JsonValue typeValue || typeValue.GetValueKind() != JsonValueKind.String ||
            identifier["id"] is not JsonValue idValue || idValue.GetValueKind() != JsonValueKind.String)
        {
            return JsonApiResults.BadRequest("Invalid resource document",
                $"The '{name}' relationship's 'data' must be null or a resource identifier object " +
                "with string 'type' and 'id' members.");
        }

        var type = typeValue.GetValue<string>();
        if (type != expectedType)
        {
            return JsonApiResults.Conflict(
                $"The '{name}' relationship expects resources of type '{expectedType}', got '{type}'.");
        }

        id = idValue.GetValue<string>();
        return null;
    }

    /// <summary>400 for relationship names the endpoint does not serve, so a misspelled
    /// relationship cannot be silently dropped.</summary>
    public IResult? RejectUnknownRelationships(params string[] known)
    {
        foreach (var (name, _) in _relationships)
        {
            if (!known.Contains(name))
            {
                return JsonApiResults.BadRequest("Invalid resource document",
                    $"Unknown relationship '{name}'. This resource's relationships: {string.Join(", ", known)}.");
            }
        }
        return null;
    }

    private static IResult? TryParse(JsonNode? body, string endpointType, out ResourceDocument document,
        out string? id)
    {
        document = null!;
        id = null;
        if (body is not JsonObject root || !root.TryGetPropertyValue("data", out var data) ||
            data is not JsonObject resource)
        {
            return JsonApiResults.BadRequest("Invalid resource document",
                "The request body must be a JSON:API document with a single resource object as its 'data' member.");
        }
        if (resource["type"] is not JsonValue typeValue || typeValue.GetValueKind() != JsonValueKind.String)
        {
            return JsonApiResults.BadRequest("Invalid resource document",
                "The resource object must contain a string 'type' member.");
        }

        var type = typeValue.GetValue<string>();
        if (type != endpointType)
        {
            return JsonApiResults.Conflict(
                $"This endpoint accepts resources of type '{endpointType}', got '{type}'.");
        }

        if (resource.TryGetPropertyValue("id", out var idNode))
        {
            if (idNode is not JsonValue idValue || idValue.GetValueKind() != JsonValueKind.String)
            {
                return JsonApiResults.BadRequest("Invalid resource document",
                    "The resource object's 'id' member must be a string.");
            }
            id = idValue.GetValue<string>();
        }

        var attributes = new JsonObject();
        if (resource.TryGetPropertyValue("attributes", out var attributesNode))
        {
            if (attributesNode is not JsonObject attributesObject)
            {
                return JsonApiResults.BadRequest("Invalid resource document",
                    "The resource object's 'attributes' member must be an object.");
            }
            attributes = attributesObject;
        }

        var relationships = new JsonObject();
        if (resource.TryGetPropertyValue("relationships", out var relationshipsNode))
        {
            if (relationshipsNode is not JsonObject relationshipsObject)
            {
                return JsonApiResults.BadRequest("Invalid resource document",
                    "The resource object's 'relationships' member must be an object.");
            }
            relationships = relationshipsObject;
        }

        document = new ResourceDocument(attributes, relationships);
        return null;
    }
}
