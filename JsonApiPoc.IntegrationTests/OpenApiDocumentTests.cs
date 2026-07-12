using System.Net;

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
}
