using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

/// <summary>Request-body shape of a to-one relationship update, used for OpenAPI schema
/// generation via <see cref="ToOneLinkageEndpointExtensions.WithToOneLinkageBody"/>. Endpoint
/// handlers bind <see cref="JsonNode"/> and call <see cref="ToOneLinkage.TryParse"/> instead,
/// because typed binding cannot distinguish a missing 'data' member (400) from an explicit
/// data: null (clear).</summary>
public sealed record ToOneLinkageDocument(ResourceIdentifier? Data);

/// <summary>Endpoint metadata declaring what a to-one linkage body targets; read by
/// JsonApiKit.OpenApi to render the request body's description and example.</summary>
public sealed record ToOneLinkageBodyMetadata(string TargetType, bool Clearable);

public static class ToOneLinkageEndpointExtensions
{
    /// <summary>Declares the endpoint's request body as a to-one linkage document targeting
    /// <paramref name="targetType"/>: sets the accepted content types (JSON:API plus plain JSON)
    /// and the metadata JsonApiKit.OpenApi turns into a schema example.</summary>
    public static RouteHandlerBuilder WithToOneLinkageBody(this RouteHandlerBuilder builder,
        string targetType, bool clearable) =>
        builder
            .Accepts<ToOneLinkageDocument>(JsonApiResults.MediaType, "application/json")
            .WithMetadata(new ToOneLinkageBodyMetadata(targetType, clearable));
}

/// <summary>Parses the body of a to-one relationship update
/// (https://jsonapi.org/format/#crud-updating-to-one-relationships): a document whose 'data'
/// member is a resource identifier object, or null to clear the relationship.</summary>
public static class ToOneLinkage
{
    /// <summary>Parses <paramref name="body"/> as a to-one linkage document for
    /// <paramref name="expectedType"/>. Returns an error result — 400 when the body is not a
    /// linkage document, 409 when the identifier's type does not match the relationship — or null
    /// on success, with <paramref name="targetId"/> set to the identifier's id, or to null when
    /// the document clears the relationship (data: null).</summary>
    public static IResult? TryParse(JsonNode? body, string expectedType, out string? targetId)
    {
        targetId = null;
        if (body is not JsonObject document || !document.TryGetPropertyValue("data", out var data))
        {
            return JsonApiResults.BadRequest("Invalid relationship document",
                "The request body must be a JSON:API to-one linkage document with a 'data' member.");
        }
        if (data is null)
        {
            return null;
        }
        if (data is not JsonObject identifier ||
            identifier["type"] is not JsonValue typeValue || typeValue.GetValueKind() != JsonValueKind.String ||
            identifier["id"] is not JsonValue idValue || idValue.GetValueKind() != JsonValueKind.String)
        {
            return JsonApiResults.BadRequest("Invalid relationship document",
                "The 'data' member must be null or a resource identifier object with string 'type' and 'id' members.");
        }

        var type = typeValue.GetValue<string>();
        if (type != expectedType)
        {
            return JsonApiResults.Conflict(
                $"This relationship expects resources of type '{expectedType}', got '{type}'.");
        }

        targetId = idValue.GetValue<string>();
        return null;
    }
}
