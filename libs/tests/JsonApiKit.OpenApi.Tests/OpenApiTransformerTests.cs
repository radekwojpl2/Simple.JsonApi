using JsonApiKit;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi.Tests;

public class OpenApiTransformerTests
{
    private static ResourceMapRegistry WidgetRegistry() => new([new WidgetMap()]);

    private static async Task<OpenApiOperation> Transform(JsonApiQueryOptions? options,
        ResourceMapRegistry? registry = null)
    {
        var services = new ServiceCollection();
        if (registry is not null)
        {
            services.AddSingleton(registry);
        }

        var operation = new OpenApiOperation();
        var context = new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new ApiDescription
            {
                ActionDescriptor = new ActionDescriptor
                {
                    EndpointMetadata = options is not null ? [options] : []
                }
            },
            ApplicationServices = services.BuildServiceProvider()
        };

        await new JsonApiQueryOperationTransformer().TransformAsync(operation, context, CancellationToken.None);
        return operation;
    }

    private static string[] ParameterNames(OpenApiOperation operation) =>
        operation.Parameters?.Select(p => p.Name!).ToArray() ?? [];

    [Fact]
    public async Task Does_nothing_without_query_metadata()
    {
        var operation = await Transform(null);

        Assert.Null(operation.Parameters);
    }

    [Fact]
    public async Task Documents_include_sort_and_page_parameters()
    {
        var operation = await Transform(new JsonApiQueryOptions
        {
            AllowedIncludes = ["company"],
            AllowedSorts = ["name"]
        });

        Assert.Equal(["include", "sort", "page[number]", "page[size]"], ParameterNames(operation));
        var include = operation.Parameters!.Single(p => p.Name == "include");
        Assert.Contains("company", include.Description);
        Assert.Equal(ParameterLocation.Query, include.In);
        Assert.False(include.Required);
    }

    [Fact]
    public async Task Page_parameters_are_integers()
    {
        var operation = await Transform(new JsonApiQueryOptions());

        var pageNumber = operation.Parameters!.Single(p => p.Name == "page[number]");
        Assert.Equal(JsonSchemaType.Integer, pageNumber.Schema!.Type);
    }

    [Fact]
    public async Task Unpaged_endpoints_document_no_page_parameters()
    {
        var operation = await Transform(new JsonApiQueryOptions { Paging = false });

        Assert.Empty(ParameterNames(operation));
    }

    [Fact]
    public async Task Filters_document_allowed_values_when_declared()
    {
        var operation = await Transform(new JsonApiQueryOptions
        {
            Paging = false,
            Filters = new Dictionary<string, IReadOnlyCollection<string>?>
            {
                ["stage"] = ["lead", "won"],
                ["search"] = null
            }
        });

        var stage = operation.Parameters!.Single(p => p.Name == "filter[stage]");
        Assert.Contains("lead, won", stage.Description);
        var search = operation.Parameters!.Single(p => p.Name == "filter[search]");
        Assert.DoesNotContain("Allowed values", search.Description);
    }

    [Fact]
    public async Task Fields_parameters_list_the_maps_fields_when_a_registry_exists()
    {
        var options = new JsonApiQueryOptions { Paging = false, FieldsTypes = ["widgets"] };

        var operation = await Transform(options, WidgetRegistry());

        var fields = operation.Parameters!.Single(p => p.Name == "fields[widgets]");
        Assert.Contains("name, note, customFields, owner, tags", fields.Description);
    }

    [Fact]
    public async Task Fields_parameters_still_appear_without_a_registry()
    {
        var options = new JsonApiQueryOptions { Paging = false, FieldsTypes = ["widgets"] };

        var operation = await Transform(options);

        var fields = operation.Parameters!.Single(p => p.Name == "fields[widgets]");
        Assert.DoesNotContain("Allowed:", fields.Description);
    }
}
