using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace JsonApiKit;

/// <summary>Enforces the server responsibilities of JSON:API content negotiation
/// (https://jsonapi.org/format/#content-negotiation-servers): 415 when the request's Content-Type
/// is anything but the JSON:API media type ("Clients and servers MUST send all JSON:API payloads
/// using the JSON:API media type in the Content-Type header" — this API serves no other body
/// contract), 415 when it is the JSON:API media type modified by disallowed parameters, and 406
/// when every JSON:API instance in Accept is so modified. 'profile' is allowed (unrecognized
/// profiles must be ignored); 'ext' is rejected because the kit supports no extensions, and the
/// spec requires 415/406 for unsupported extension URIs; 'q' is HTTP's quality weight
/// (RFC 9110 §12.4.2), not a media type parameter, so it does not modify the media type in the
/// spec's sense.</summary>
public sealed class JsonApiContentNegotiationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.GetTypedHeaders();

        var contentType = headers.ContentType;
        if (contentType is not null && !IsJsonApi(contentType))
        {
            await Reject(context, StatusCodes.Status415UnsupportedMediaType, "Unsupported media type",
                "This API accepts only JSON:API request bodies; send the payload as " +
                $"'{JsonApiResults.MediaType}'.");
            return;
        }
        if (contentType is not null && IsModified(contentType))
        {
            await Reject(context, StatusCodes.Status415UnsupportedMediaType, "Unsupported media type",
                "The JSON:API media type in Content-Type must not be modified by media type " +
                "parameters other than 'profile'; extensions are not supported.");
            return;
        }

        var jsonApiAccepts = headers.Accept.Where(IsJsonApi).ToList();
        if (jsonApiAccepts.Count > 0 && jsonApiAccepts.All(IsModified))
        {
            await Reject(context, StatusCodes.Status406NotAcceptable, "Not acceptable",
                "Accept offers the JSON:API media type only in instances modified by media type " +
                "parameters other than 'profile'; extensions are not supported.");
            return;
        }

        await next(context);
    }

    private static bool IsJsonApi(MediaTypeHeaderValue mediaType) =>
        mediaType.MediaType.Equals(JsonApiResults.MediaType, StringComparison.OrdinalIgnoreCase);

    private static bool IsModified(MediaTypeHeaderValue mediaType) =>
        mediaType.Parameters.Any(parameter =>
            !parameter.Name.Equals("profile", StringComparison.OrdinalIgnoreCase) &&
            !parameter.Name.Equals("q", StringComparison.OrdinalIgnoreCase));

    private static Task Reject(HttpContext context, int statusCode, string title, string detail) =>
        JsonApiResults.Error(new JsonApiError { StatusCode = statusCode, Title = title, Detail = detail })
            .ExecuteAsync(context);
}
