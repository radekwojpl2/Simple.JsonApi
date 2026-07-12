using System.Text.Json.Nodes;
using JsonApiKit;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi.Tests;

public class ToOneLinkageTransformerTests
{
    private static async Task<OpenApiOperation> Transform(ToOneLinkageBodyMetadata? metadata)
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [JsonApiResults.MediaType] = new(),
                    ["application/json"] = new()
                }
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

        await new ToOneLinkageOperationTransformer().TransformAsync(operation, context, CancellationToken.None);
        return operation;
    }

    [Fact]
    public async Task Linkage_bodies_get_a_description_and_a_typed_example_on_every_content_type()
    {
        var operation = await Transform(new ToOneLinkageBodyMetadata("contacts", Clearable: true));

        Assert.Contains("null to clear", operation.RequestBody!.Description);
        foreach (var mediaType in operation.RequestBody.Content!.Values)
        {
            var example = Assert.IsType<JsonObject>(mediaType.Example);
            Assert.Equal("contacts", example["data"]!["type"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Required_linkage_bodies_say_data_must_not_be_null()
    {
        var operation = await Transform(new ToOneLinkageBodyMetadata("companies", Clearable: false));

        Assert.Contains("must not be null", operation.RequestBody!.Description);
        Assert.DoesNotContain("null to clear", operation.RequestBody.Description);
    }

    [Fact]
    public async Task Operations_without_linkage_metadata_are_untouched()
    {
        var operation = await Transform(null);

        Assert.Null(operation.RequestBody!.Description);
        Assert.All(operation.RequestBody.Content!.Values, mediaType => Assert.Null(mediaType.Example));
    }
}
