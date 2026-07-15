using System.Net;
using System.Net.Http.Headers;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>Checks the server responsibilities of the JSON:API 1.1 "Content Negotiation" section
/// (https://jsonapi.org/format/#content-negotiation). Each test quotes verbatim the sentence it
/// enforces.</summary>
[Collection(ApiCollection.Name)]
public class ContentNegotiationSpecComplianceTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>"Clients and servers MUST send all JSON:API payloads using the JSON:API media type
    /// (application/vnd.api+json) in the Content-Type header."</summary>
    [Fact]
    public async Task Responses_CarryTheJsonApiMediaType()
    {
        // Arrange
        var deal = await ArrangeAsync(OneDeal);

        // Act
        var response = await Client.GetAsync($"{Routes.Deals}/{deal.Id}");

        // Assert
        Assert.Equal(JsonApiMediaTypes.JsonApi, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>"Clients and servers MUST send all JSON:API payloads using the JSON:API media type
    /// (application/vnd.api+json) in the Content-Type header." — this server enforces the client
    /// half by refusing any other body media type with 415, since it serves no other contract.</summary>
    [Fact]
    public async Task NonJsonApiContentType_Returns415()
    {
        // Arrange — a payload declared as plain JSON.
        using var request = new HttpRequestMessage(HttpMethod.Post, Routes.Deals);
        request.Content = new StringContent("{}");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>"If a request specifies the Content-Type header with the JSON:API media type,
    /// servers MUST respond with a 415 Unsupported Media Type status code if that media type
    /// contains any media type parameters other than ext or profile."</summary>
    [Fact]
    public async Task JsonApiContentTypeWithOtherParameters_Returns415()
    {
        // Arrange — a request whose Content-Type is the JSON:API media type plus a parameter the
        // spec does not allow (charset is neither ext nor profile).
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Content = new StringContent("");
        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue(JsonApiMediaTypes.JsonApi) { CharSet = "utf-8" };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>"If all instances of that [JSON:API] media type [in the Accept header] are
    /// modified with a media type parameter other than ext or profile, servers MUST respond with
    /// a 406 Not Acceptable status code."</summary>
    [Fact]
    public async Task AcceptWithOnlyModifiedJsonApiInstances_Returns406()
    {
        // Arrange — every JSON:API instance in Accept carries a disallowed parameter.
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Headers.Accept.ParseAdd($"{JsonApiMediaTypes.JsonApi}; unsupported=yes");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    /// <summary>"If a request's Accept header contains an instance of the JSON:API media type,
    /// servers MUST ignore instances of that media type which are modified by a media type
    /// parameter other than ext or profile." — with one clean instance alongside a modified one,
    /// the request succeeds.</summary>
    [Fact]
    public async Task AcceptWithACleanJsonApiInstanceAmongModifiedOnes_Succeeds()
    {
        // Arrange
        await ArrangeAsync(OneDeal);
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Headers.Accept.ParseAdd($"{JsonApiMediaTypes.JsonApi}; unsupported=yes");
        request.Headers.Accept.ParseAdd(JsonApiMediaTypes.JsonApi);

        // Act
        var response = await Client.SendAsync(request);

        // Assert — the modified instance is ignored, the clean one is served.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonApiMediaTypes.JsonApi, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>"If a request specifies the Content-Type header with an instance of the JSON:API
    /// media type modified by the ext media type parameter and that parameter contains an
    /// unsupported extension URI, the server MUST respond with a 415" — this server supports no
    /// extensions, so every ext URI is unsupported.</summary>
    [Fact]
    public async Task JsonApiContentTypeWithExtParameter_Returns415()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Content = new StringContent("");
        var contentType = new MediaTypeHeaderValue(JsonApiMediaTypes.JsonApi);
        contentType.Parameters.Add(new NameValueHeaderValue("ext", "\"https://example.com/ext\""));
        request.Content.Headers.ContentType = contentType;

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>"If every instance of that media type is modified by the ext parameter and each
    /// contains at least one unsupported extension URI, the server MUST also respond with a
    /// 406 Not Acceptable."</summary>
    [Fact]
    public async Task AcceptWithOnlyExtModifiedJsonApiInstances_Returns406()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Headers.Accept.ParseAdd($"{JsonApiMediaTypes.JsonApi}; ext=\"https://example.com/ext\"");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    /// <summary>"A server MUST ignore any profiles that it does not recognize." — a profile
    /// parameter never causes a rejection, on Content-Type or Accept.</summary>
    [Fact]
    public async Task ProfileParameter_IsIgnoredAndTheRequestSucceeds()
    {
        // Arrange
        await ArrangeAsync(OneDeal);
        using var request = new HttpRequestMessage(HttpMethod.Get, Routes.Deals);
        request.Headers.Accept.ParseAdd($"{JsonApiMediaTypes.JsonApi}; profile=\"https://example.com/profile\"");
        request.Content = new StringContent("");
        var contentType = new MediaTypeHeaderValue(JsonApiMediaTypes.JsonApi);
        contentType.Parameters.Add(new NameValueHeaderValue("profile", "\"https://example.com/profile\""));
        request.Content.Headers.ContentType = contentType;

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonApiMediaTypes.JsonApi, response.Content.Headers.ContentType?.MediaType);
    }

    private static Deal OneDeal(AppDbContext db) =>
        db.Deals.Add(Rows.Deal("ERP integration", Rows.Company(), Rows.User())).Entity;
}
