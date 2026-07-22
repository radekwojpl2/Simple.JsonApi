using System.Text;
using JsonApiLite;

namespace JsonApiPoc.Api;

/// <summary>The whole ASP.NET seam. JsonApiLite models documents and nothing else, so bodies are
/// read and written as text with the JSON:API media type — no formatters, no model binding.</summary>
public static class JsonApi
{
    public static async Task<TDocument?> Read<TDocument>(HttpContext http)
    {
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }
        return JsonApiSerializer.Deserialize<TDocument>(body);
    }

    public static IResult Ok<TDocument>(TDocument document) =>
        Write(document, StatusCodes.Status200OK);

    public static IResult Created<TDocument>(TDocument document) =>
        Write(document, StatusCodes.Status201Created);

    public static IResult NotFound(string detail) =>
        Write(
            Problem(status: "404", title: "Resource not found", detail: detail, pointer: null),
            StatusCodes.Status404NotFound);

    public static IResult Malformed(string detail) =>
        Write(
            Problem(status: "400", title: "Malformed document", detail: detail, pointer: null),
            StatusCodes.Status400BadRequest);

    public static IResult ServerError() =>
        Write(
            Problem(status: "500", title: "Internal server error", detail: "Unexpected failure.", pointer: null),
            StatusCodes.Status500InternalServerError);

    public static IResult Invalid(string pointer, string detail) =>
        Write(
            Problem(status: "422", title: "Validation failed", detail: detail, pointer: pointer),
            StatusCodes.Status422UnprocessableEntity);

    private static ErrorDocument Problem(string status, string title, string detail, string? pointer)
    {
        var error = new Error { Status = status, Title = title, Detail = detail };
        if (pointer is not null)
        {
            error = error with { Source = new ErrorSource { Pointer = pointer } };
        }
        return new ErrorDocument { Errors = [error] };
    }

    private static IResult Write<TDocument>(TDocument document, int statusCode) =>
        Results.Text(
            JsonApiSerializer.Serialize(document),
            JsonApiMediaType.Value,
            Encoding.UTF8,
            statusCode);
}
