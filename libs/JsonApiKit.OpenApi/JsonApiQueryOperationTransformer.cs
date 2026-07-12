using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi;

public sealed class JsonApiQueryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var options = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<JsonApiQueryOptions>().FirstOrDefault();
        if (options is null)
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];

        if (options.AllowedIncludes.Count > 0)
        {
            Add(operation, "include", JsonSchemaType.String,
                $"Comma-separated relationship paths to side-load. Allowed: {string.Join(", ", options.AllowedIncludes)}.");
        }

        if (options.AllowedSorts.Count > 0)
        {
            Add(operation, "sort", JsonSchemaType.String,
                $"Comma-separated sort fields; prefix a field with '-' for descending. Allowed: {string.Join(", ", options.AllowedSorts)}.");
        }

        if (options.Paging)
        {
            Add(operation, "page[number]", JsonSchemaType.Integer, "1-based page number. Default 1.");
            Add(operation, "page[size]", JsonSchemaType.Integer, "Resources per page.");
        }

        foreach (var (name, allowedValues) in options.Filters)
        {
            Add(operation, $"filter[{name}]", JsonSchemaType.String, allowedValues is null
                ? $"Filter by {name}."
                : $"Filter by {name}. Allowed values: {string.Join(", ", allowedValues)}.");
        }

        var registry = context.ApplicationServices.GetService<ResourceMapRegistry>();
        foreach (var resourceType in options.FieldsTypes)
        {
            var fields = registry is not null && registry.TryGet(resourceType, out var map)
                ? $" Allowed: {string.Join(", ", map.Fields)}."
                : "";
            Add(operation, $"fields[{resourceType}]", JsonSchemaType.String,
                $"Sparse fieldset: comma-separated field names to return for {resourceType}.{fields}");
        }

        return Task.CompletedTask;
    }

    private static void Add(OpenApiOperation operation, string name, JsonSchemaType type, string description) =>
        operation.Parameters!.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = type }
        });
}
