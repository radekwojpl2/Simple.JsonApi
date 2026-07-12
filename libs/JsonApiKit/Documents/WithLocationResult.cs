using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

/// <summary>Wraps a result to add a Location header, for 201 Created responses.</summary>
internal sealed class WithLocationResult(string location, IResult inner) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.Location = location;
        return inner.ExecuteAsync(httpContext);
    }
}
