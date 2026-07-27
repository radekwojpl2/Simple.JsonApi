using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace JsonApiLite.OpenApi;

/// <summary>Reads <see cref="JsonApiBody"/> metadata off each operation and replaces the framework's
/// empty request/response schema with one built from the attribute and relationship types.</summary>
internal sealed class JsonApiOperationTransformer(JsonSerializerOptions serializerOptions)
    : IOpenApiOperationTransformer
{
    private readonly JsonApiSchemaBuilder schemas = new(serializerOptions);

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var bodies = context.Description.ActionDescriptor.EndpointMetadata.OfType<JsonApiBody>();
        foreach (var body in bodies)
        {
            var media = new OpenApiMediaType { Schema = schemas.Document(body) };
            var content = new Dictionary<string, OpenApiMediaType> { [JsonApiMediaType.Value] = media };

            if (body is JsonApiResponseBody response)
            {
                operation.Responses ??= new OpenApiResponses();
                operation.Responses[response.StatusCode.ToString(CultureInfo.InvariantCulture)] =
                    new OpenApiResponse
                    {
                        Description = ReasonPhrase(response.StatusCode),
                        Content = content,
                    };
                continue;
            }

            operation.RequestBody = new OpenApiRequestBody { Required = true, Content = content };
        }

        return Task.CompletedTask;
    }

    private static string ReasonPhrase(int statusCode)
    {
        var phrase = ReasonPhrases.GetReasonPhrase(statusCode);
        if (string.IsNullOrEmpty(phrase))
        {
            return "Response";
        }

        return phrase;
    }
}
