using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiKit.Testing;

/// <summary>HttpClient extensions for driving a JSON:API server in integration tests.</summary>
public static class JsonApiHttpClientExtensions
{
    /// <summary>GETs <paramref name="url"/> and parses the body as JSON. Non-success responses
    /// throw with the status code and body in the message, so a failing test shows the server's
    /// actual error instead of a bare status. The JSON:API media type is asserted on every call —
    /// it is a spec invariant of successful responses, so no test needs to check it by hand.</summary>
    public static async Task<JsonNode> GetDocumentAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GET {url} returned {(int)response.StatusCode}: {body}");
        }
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != JsonApiMediaTypes.JsonApi)
        {
            throw new HttpRequestException(
                $"GET {url} returned media type '{mediaType}' instead of '{JsonApiMediaTypes.JsonApi}'.");
        }
        return JsonNode.Parse(body)
               ?? throw new InvalidOperationException($"GET {url} returned an empty body.");
    }

    /// <summary>GETs <paramref name="url"/> expecting an error: asserts the status code and the
    /// problem-details media type, then returns the parsed body so the test can match the full
    /// problem document. The per-request traceId member is asserted present and stripped — it is
    /// random diagnostics, not part of the API contract an exact match can pin.</summary>
    public static async Task<JsonNode> GetProblemAsync(this HttpClient client, string url, int expectedStatus) =>
        await (await client.GetAsync(url)).ReadProblemAsync(expectedStatus);

    /// <summary>Asserts an already-received response is a problem-details error with
    /// <paramref name="expectedStatus"/> and returns its parsed body, for POST/PATCH/DELETE error
    /// paths. Same traceId handling as <see cref="GetProblemAsync"/>.</summary>
    public static async Task<JsonNode> ReadProblemAsync(this HttpResponseMessage response, int expectedStatus)
    {
        var url = response.RequestMessage?.RequestUri?.PathAndQuery ?? "(unknown)";
        var body = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expectedStatus)
        {
            throw new HttpRequestException(
                $"{url} returned {(int)response.StatusCode}, expected {expectedStatus}: {body}");
        }
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != JsonApiMediaTypes.ProblemDetails)
        {
            throw new HttpRequestException(
                $"{url} returned media type '{mediaType}' instead of '{JsonApiMediaTypes.ProblemDetails}'.");
        }
        var problem = JsonNode.Parse(body)?.AsObject()
                      ?? throw new InvalidOperationException($"{url} returned an empty body.");
        if (!problem.Remove("traceId"))
        {
            throw new InvalidOperationException($"{url} returned a problem document without a traceId.");
        }
        return problem;
    }

    /// <summary>Asserts the spec's successful-write contract for updates and deletions
    /// (https://jsonapi.org/format/#crud): 200 OK with a response document, or 204 No Content.</summary>
    public static void ShouldBeSuccessfulWrite(this HttpResponseMessage response)
    {
        var url = response.RequestMessage?.RequestUri?.PathAndQuery ?? "(unknown)";
        if (response.StatusCode == HttpStatusCode.OK)
        {
            if (response.Content.Headers.ContentLength is not > 0)
            {
                throw new HttpRequestException(
                    $"{url} returned 200 OK without a body; a 200 write response must carry a response document.");
            }
            return;
        }
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new HttpRequestException(
                $"{url} returned {(int)response.StatusCode}, expected a successful write: 200 with a document, or 204.");
        }
    }

    /// <summary>POSTs <paramref name="document"/> serialized as a JSON:API request body:
    /// application/vnd.api+json with no media type parameters (a charset parameter would rightly
    /// draw a 415).</summary>
    public static Task<HttpResponseMessage> PostJsonApiAsync(this HttpClient client, string url,
        object document) =>
        client.PostAsync(url, JsonApiContent(document));

    /// <inheritdoc cref="PostJsonApiAsync"/>
    public static Task<HttpResponseMessage> PatchJsonApiAsync(this HttpClient client, string url,
        object document) =>
        client.PatchAsync(url, JsonApiContent(document));

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static HttpContent JsonApiContent(object document)
    {
        var content = new StringContent(JsonSerializer.Serialize(document, Web), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(JsonApiMediaTypes.JsonApi);
        return content;
    }

    /// <summary>Id of the first resource in the collection at <paramref name="collectionUrl"/>
    /// whose string attribute <paramref name="attribute"/> equals <paramref name="value"/>.
    /// Only inspects the returned page — pass a page-size parameter if the collection is larger.</summary>
    public static async Task<string> FindIdAsync(this HttpClient client, string collectionUrl,
        string attribute, string value)
    {
        var document = await client.GetDocumentAsync(collectionUrl);
        var resource = document[JsonApiMember.Data]?.AsArray()
            .FirstOrDefault(r => r?[JsonApiMember.Attributes]?[attribute]?.GetValue<string>() == value);
        return resource?[JsonApiMember.Id]?.GetValue<string>()
               ?? throw new InvalidOperationException(
                   $"No resource in '{collectionUrl}' has {attribute} == '{value}'.");
    }
}
