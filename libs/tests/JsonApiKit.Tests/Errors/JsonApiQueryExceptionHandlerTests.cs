using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit.Tests;

public class JsonApiQueryExceptionHandlerTests
{
    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task Handles_query_exceptions_as_problem_details()
    {
        var handler = new JsonApiQueryExceptionHandler();
        var context = Context();
        var exception = new JsonApiQueryException(new JsonApiError
        {
            StatusCode = 400,
            Title = "Invalid sort",
            Detail = "Unsupported sort field 'height'."
        });

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task Ignores_unrelated_exceptions()
    {
        var handler = new JsonApiQueryExceptionHandler();
        var context = Context();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(200, context.Response.StatusCode); // untouched
    }
}
