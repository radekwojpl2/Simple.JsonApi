using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit.Tests;

public class JsonApiResultsTests
{
    private static async Task<(HttpContext Context, string Body)> Execute(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context, body);
    }

    [Fact]
    public void Ok_produces_200_with_jsonapi_media_type()
    {
        var result = JsonApiResults.Ok(new JsonApiDocument());

        var json = Assert.IsType<JsonHttpResult<JsonApiDocument>>(result);
        Assert.Equal(200, json.StatusCode);
        Assert.Equal(JsonApiResults.MediaType, json.ContentType);
    }

    [Fact]
    public void Ok_for_to_one_documents_uses_jsonapi_media_type()
    {
        var result = JsonApiResults.Ok(new JsonApiToOneDocument());

        var json = Assert.IsType<JsonHttpResult<JsonApiToOneDocument>>(result);
        Assert.Equal(JsonApiResults.MediaType, json.ContentType);
    }

    [Fact]
    public void Result_honors_a_custom_status_code()
    {
        var result = JsonApiResults.Result(new JsonApiDocument(), StatusCodes.Status202Accepted);

        Assert.Equal(202, Assert.IsType<JsonHttpResult<JsonApiDocument>>(result).StatusCode);
    }

    [Fact]
    public async Task Created_sets_location_status_and_media_type()
    {
        var document = new JsonApiDocument { Data = new ResourceObject("widgets", "7") };

        var (context, body) = await Execute(JsonApiResults.Created("/api/widgets/7", document));

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal("/api/widgets/7", context.Response.Headers.Location);
        Assert.Equal(JsonApiResults.MediaType, context.Response.ContentType);
        Assert.Contains("\"id\":\"7\"", body);
    }

    [Fact]
    public async Task Executed_document_omits_null_members_on_the_wire()
    {
        var (_, body) = await Execute(JsonApiResults.Ok(new JsonApiDocument { Data = new List<ResourceObject>() }));

        Assert.Equal("""{"data":[]}""", body);
    }
}
