using Microsoft.AspNetCore.OpenApi;

namespace JsonApiKit.OpenApi;

public static class JsonApiOpenApiExtensions
{
    /// <summary>Documents the JSON:API query parameters (include, sort, page, filter) that
    /// <see cref="JsonApiQuery"/> binds, by reading each endpoint's <see cref="JsonApiQueryOptions"/>
    /// metadata. BindAsync parameters are invisible to OpenAPI generation without this.</summary>
    public static OpenApiOptions AddJsonApiQueryParameters(this OpenApiOptions options)
    {
        options.AddOperationTransformer<JsonApiQueryOperationTransformer>();
        return options;
    }

    /// <summary>Documents to-one relationship update bodies declared via
    /// <see cref="ToOneLinkageEndpointExtensions.WithToOneLinkageBody"/> with a linkage
    /// description and a per-relationship request example.</summary>
    public static OpenApiOptions AddJsonApiLinkageBodies(this OpenApiOptions options)
    {
        options.AddOperationTransformer<ToOneLinkageOperationTransformer>();
        return options;
    }

    /// <summary>Documents dual-contract write bodies declared via
    /// <see cref="ResourceDocumentEndpointExtensions.WithResourceDocumentBody"/>: the flat request
    /// schema for application/json and a JSON:API resource-document schema for
    /// application/vnd.api+json, which JsonNode-bound endpoints otherwise leave untyped.</summary>
    public static OpenApiOptions AddJsonApiResourceDocumentBodies(this OpenApiOptions options)
    {
        options.AddOperationTransformer<ResourceDocumentOperationTransformer>();
        return options;
    }
}
