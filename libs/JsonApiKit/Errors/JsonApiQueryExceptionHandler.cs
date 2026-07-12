using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

/// <summary>Maps <see cref="JsonApiQueryException"/> thrown during parameter binding to a 400
/// problem-details response. Registered by AddJsonApi; runs inside the app's UseExceptionHandler
/// pipeline.</summary>
public sealed class JsonApiQueryExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not JsonApiQueryException queryException)
        {
            return false;
        }

        await JsonApiResults.Error(queryException.Error).ExecuteAsync(httpContext);
        return true;
    }
}
