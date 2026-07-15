using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JsonApiKit.OpenApi;

/// <summary>Documents JSON:API write bodies declared via
/// <see cref="ResourceDocumentEndpointExtensions.WithResourceDocumentBody"/>. The endpoint binds
/// JsonNode (it has to inspect the document before typing it), which would otherwise surface as an
/// untyped schema; this transformer replaces it with a JSON:API resource-document schema.</summary>
public sealed class ResourceDocumentOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<ResourceDocumentBodyMetadata>().FirstOrDefault();
        if (metadata is null || operation.RequestBody is not { Content: { } content })
        {
            return;
        }

        operation.RequestBody.Description = metadata.RequiresId
            ? $"JSON:API resource document of type '{metadata.ResourceType}' whose id matches the " +
              "URL; omitted members keep their current values."
            : $"JSON:API resource document of type '{metadata.ResourceType}'.";

        if (content.TryGetValue(JsonApiResults.MediaType, out var document))
        {
            document.Schema = await BuildDocumentSchema(metadata, context, cancellationToken);
        }
    }

    private static async Task<OpenApiSchema> BuildDocumentSchema(ResourceDocumentBodyMetadata metadata,
        OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var resource = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string>(metadata.RequiresId ? ["type", "id"] : ["type"]),
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["type"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = $"Always '{metadata.ResourceType}'."
                }
            }
        };
        if (metadata.RequiresId)
        {
            resource.Properties["id"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Must match the id addressed by the URL."
            };
        }
        var attributes = await context.GetOrCreateSchemaAsync(metadata.AttributesType,
            cancellationToken: cancellationToken);
        if (attributes is OpenApiSchema inlineAttributes)
        {
            // The schema exporter marks every positional record parameter required, but attributes
            // are all optional: missing ones default on create and keep their values on update.
            inlineAttributes.Required = null;
        }
        resource.Properties["attributes"] = attributes;
        if (metadata.Relationships.Count > 0)
        {
            resource.Properties["relationships"] = Relationships(metadata.Relationships);
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "data" },
            Properties = new Dictionary<string, IOpenApiSchema> { ["data"] = resource }
        };
    }

    private static OpenApiSchema Relationships(IReadOnlyList<ResourceDocumentRelationshipMetadata> relationships)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
            Required = new HashSet<string>()
        };
        foreach (var relationship in relationships)
        {
            schema.Properties[relationship.Name] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "data" },
                Properties = new Dictionary<string, IOpenApiSchema> { ["data"] = Identifier(relationship) }
            };
            if (relationship.Required)
            {
                schema.Required.Add(relationship.Name);
            }
        }
        return schema;
    }

    private static OpenApiSchema Identifier(ResourceDocumentRelationshipMetadata relationship) => new()
    {
        Type = relationship.Clearable ? JsonSchemaType.Object | JsonSchemaType.Null : JsonSchemaType.Object,
        Required = new HashSet<string> { "type", "id" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = $"Always '{relationship.TargetType}'."
            },
            ["id"] = new OpenApiSchema { Type = JsonSchemaType.String }
        },
        Description = relationship.Clearable
            ? $"Resource identifier of type '{relationship.TargetType}', or null to clear the relationship."
            : $"Resource identifier of type '{relationship.TargetType}'."
    };
}
