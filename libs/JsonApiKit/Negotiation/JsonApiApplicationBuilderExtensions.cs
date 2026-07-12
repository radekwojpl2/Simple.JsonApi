using Microsoft.AspNetCore.Builder;

namespace JsonApiKit;

public static class JsonApiApplicationBuilderExtensions
{
    /// <summary>Adds the JSON:API content-negotiation checks (415/406,
    /// https://jsonapi.org/format/#content-negotiation-servers). Place it before endpoint routing
    /// so non-negotiable requests never reach a handler.</summary>
    public static IApplicationBuilder UseJsonApiContentNegotiation(this IApplicationBuilder app) =>
        app.UseMiddleware<JsonApiContentNegotiationMiddleware>();
}
