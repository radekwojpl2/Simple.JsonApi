using System.Net;
using System.Text.Json.Nodes;

namespace JsonApiPoc.IntegrationTests;

/// <summary>The OpenAPI document is generated at runtime from real endpoint metadata, so serving it
/// exercises the full JsonApiKit.OpenApi integration in a way unit tests can't.</summary>
[Collection(ApiCollection.Name)]
public class OpenApiDocumentTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Document_InDevelopment_IsServedAsJson()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Document_DescribesJsonApiQueryParameters()
    {
        // Act
        var body = await (await _client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("page[number]", body);
        Assert.Contains("page[size]", body);
        Assert.Contains("filter[stage]", body);
        Assert.Contains($"fields[{ResourceTypes.Deals}]", body);
        Assert.Contains("include", body);
    }

    [Fact]
    public async Task Document_DescribesRelationshipUpdateBodies()
    {
        // Act
        var body = await (await _client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();

        // Assert — the linkage description and example rendered by AddJsonApiLinkageBodies.
        Assert.Contains("To-one linkage document", body);
        Assert.Contains("null to clear the relationship", body);
    }

    /// <summary>The write endpoints bind JsonNode, so their body schemas come from
    /// AddJsonApiResourceDocumentBodies — without it Swagger shows an untyped body.</summary>
    [Fact]
    public async Task Document_DescribesWriteBodiesAsResourceDocuments()
    {
        // Act
        var body = await (await _client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();
        var requestBody = JsonNode.Parse(body)!
            ["paths"]!["/api/deals"]!["post"]!["requestBody"]!.AsObject();

        // Assert — JSON:API is the only write contract; no flat application/json body is offered.
        Assert.False(requestBody["content"]!.AsObject().ContainsKey("application/json"));

        // Assert — the body is a resource document: data with type, attributes (deal fields, no
        // foreign keys), and relationships with required company/owner linkage.
        var document = Resolve(JsonNode.Parse(body)!, requestBody["content"]!["application/vnd.api+json"]!["schema"]!);
        var resource = Resolve(JsonNode.Parse(body)!, document["properties"]!["data"]!);
        Assert.Equal("Always 'deals'.", resource["properties"]!["type"]!["description"]!.GetValue<string>());
        var attributes = Resolve(JsonNode.Parse(body)!, resource["properties"]!["attributes"]!);
        Assert.True(attributes["properties"]!.AsObject().ContainsKey("title"));
        Assert.False(attributes["properties"]!.AsObject().ContainsKey("companyId"));
        var relationships = resource["properties"]!["relationships"]!;
        Assert.True(relationships["properties"]!.AsObject().ContainsKey("company"));
        Assert.Contains("company", relationships["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.DoesNotContain("contact", relationships["required"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    /// <summary>Follows a $ref into components.schemas; inline schemas come back unchanged.</summary>
    private static JsonNode Resolve(JsonNode document, JsonNode schema)
    {
        if (schema["$ref"] is not { } reference)
        {
            return schema;
        }
        var name = reference.GetValue<string>().Split('/')[^1];
        return document["components"]!["schemas"]![name]!;
    }
}
