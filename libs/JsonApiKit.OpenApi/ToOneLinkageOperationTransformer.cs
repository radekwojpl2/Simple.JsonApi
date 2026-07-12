using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi;

/// <summary>Documents to-one relationship update bodies declared via
/// <see cref="ToOneLinkageEndpointExtensions.WithToOneLinkageBody"/>: the request body gets a
/// description of the linkage contract and a concrete example for the endpoint's target type,
/// which the schema alone cannot convey.</summary>
public sealed class ToOneLinkageOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var linkage = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<ToOneLinkageBodyMetadata>().FirstOrDefault();
        if (linkage is null || operation.RequestBody is not { Content: { } content })
        {
            return Task.CompletedTask;
        }

        operation.RequestBody.Description = linkage.Clearable
            ? $"To-one linkage document: 'data' is a resource identifier of type '{linkage.TargetType}', or null to clear the relationship."
            : $"To-one linkage document: 'data' is a resource identifier of type '{linkage.TargetType}'. The relationship is required, so data must not be null.";

        foreach (var mediaType in content.Values)
        {
            mediaType.Example = new JsonObject
            {
                ["data"] = new JsonObject { ["type"] = linkage.TargetType, ["id"] = "1" }
            };
        }

        return Task.CompletedTask;
    }
}
