using System.Text.Json;
using JsonApiLite.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;

// Deliberately the core library's namespace, not the assembly's: a consumer already has
// 'using JsonApiLite;' for the document types, and these extensions are only useful next to them.
namespace JsonApiLite;

/// <summary>
/// Teaches OpenAPI the shape of the JSON:API bodies. The document types (ResourceDocument&lt;&gt;,
/// Optional&lt;T&gt;, and friends) carry custom converters, and the schema generator treats a
/// converter as opaque — so a bound body like <c>ResourceDocument&lt;ContactAttributes,
/// ContactRelationships&gt;</c> renders as an empty <c>{}</c>. These extensions describe the body
/// from the attribute and relationship types instead, and an operation transformer writes that
/// schema onto the request or response.
/// </summary>
public static class JsonApiOpenApi
{
    /// <summary>Register the transformer that turns the annotations below into schemas. Call this
    /// from <c>AddOpenApi</c>.</summary>
    /// <param name="serializerOptions">The options the app actually serializes with, so the
    /// generated schema agrees with the wire on member naming and enum representation. Defaults
    /// to <see cref="JsonApiSerializer.Options"/>; pass the app's own if it configured its
    /// own.</param>
    public static void UseJsonApiBodies(
        this OpenApiOptions options, JsonSerializerOptions? serializerOptions = null) =>
        options.AddOperationTransformer(
            new JsonApiOperationTransformer(serializerOptions ?? JsonApiSerializer.Options));

    /// <summary>Declare the JSON:API document this endpoint accepts, named the same way the handler
    /// binds it — <c>ResourceDocument&lt;ContactAttributes, ContactRelationships&gt;</c>. A create
    /// omits the id; a PATCH carries it (<paramref name="includeId"/>) and, through Optional&lt;T&gt;
    /// attributes, treats every member as optional — the tri-state on the wire.</summary>
    public static RouteHandlerBuilder AcceptsJsonApi<TDocument>(
        this RouteHandlerBuilder builder, bool includeId = false) =>
        builder.WithMetadata(JsonApiBody.Request(typeof(TDocument), includeId));

    /// <summary>Declare the JSON:API document this endpoint returns at <paramref name="statusCode"/>,
    /// named the same way the handler builds it. A collection document is recognised on its own, and
    /// a returned resource always carries its id.</summary>
    public static RouteHandlerBuilder ProducesJsonApi<TDocument>(
        this RouteHandlerBuilder builder, int statusCode = StatusCodes.Status200OK) =>
        builder
            .Produces(statusCode, contentType: JsonApiMediaType.Value)
            .WithMetadata(JsonApiBody.Response(typeof(TDocument), statusCode));

    /// <summary>Declare that this endpoint answers <paramref name="statusCode"/> with an
    /// <see cref="ErrorDocument"/> — the failure paths a JSON:API caller has to handle, which the
    /// framework would otherwise describe as an untyped response.</summary>
    public static RouteHandlerBuilder ProducesJsonApiError(
        this RouteHandlerBuilder builder, int statusCode) =>
        builder
            .Produces(statusCode, contentType: JsonApiMediaType.Value)
            .WithMetadata(JsonApiBody.Response(typeof(ErrorDocument), statusCode));
}
