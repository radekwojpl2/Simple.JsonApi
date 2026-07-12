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
}
