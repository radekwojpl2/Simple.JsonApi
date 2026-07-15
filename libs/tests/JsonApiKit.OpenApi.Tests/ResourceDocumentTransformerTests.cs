using JsonApiKit;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi.Tests;

/// <summary>Covers the transformer's metadata handling; the schemas themselves are asserted
/// against the real generation pipeline in the host app's OpenApiDocumentTests, because
/// GetOrCreateSchemaAsync needs the services a hand-built context cannot provide.</summary>
public class ResourceDocumentTransformerTests
{
    private sealed record WidgetWriteRequest(string? Name);

    private static async Task<OpenApiOperation> Transform(ResourceDocumentBodyMetadata? metadata)
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                // No content types: the metadata paths that need schema generation stay untouched.
                Content = new Dictionary<string, OpenApiMediaType>()
            }
        };
        var context = new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new ApiDescription
            {
                ActionDescriptor = new ActionDescriptor
                {
                    EndpointMetadata = metadata is not null ? [metadata] : []
                }
            },
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await new ResourceDocumentOperationTransformer().TransformAsync(operation, context, CancellationToken.None);
        return operation;
    }

    [Fact]
    public async Task Create_bodies_describe_the_resource_document()
    {
        var operation = await Transform(new ResourceDocumentBodyMetadata(
            "widgets", typeof(WidgetWriteRequest), RequiresId: false, []));

        Assert.Contains("JSON:API resource document", operation.RequestBody!.Description);
        Assert.Contains("'widgets'", operation.RequestBody.Description);
        Assert.DoesNotContain("id matches the URL", operation.RequestBody.Description);
    }

    [Fact]
    public async Task Update_bodies_say_the_id_must_match_the_url()
    {
        var operation = await Transform(new ResourceDocumentBodyMetadata(
            "widgets", typeof(WidgetWriteRequest), RequiresId: true, []));

        Assert.Contains("id matches the URL", operation.RequestBody!.Description);
    }

    [Fact]
    public async Task Operations_without_resource_document_metadata_are_untouched()
    {
        var operation = await Transform(null);

        Assert.Null(operation.RequestBody!.Description);
    }
}
