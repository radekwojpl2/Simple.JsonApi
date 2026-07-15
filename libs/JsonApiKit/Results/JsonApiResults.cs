using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

public static class JsonApiResults
{
    public const string MediaType = "application/vnd.api+json";

    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IResult Result(JsonApiDocument document, int statusCode = StatusCodes.Status200OK) =>
        Results.Json(document, SerializerOptions, MediaType, statusCode);

    public static IResult Ok(JsonApiDocument document) => Result(document);

    public static IResult Ok(JsonApiToOneDocument document) =>
        Results.Json(document, SerializerOptions, MediaType);

    /// <summary>201 with the created resource as primary data, as the spec requires when the server
    /// assigns the id (https://jsonapi.org/format/#crud-creating-responses-201).</summary>
    public static IResult Created(string location, JsonApiDocument document) =>
        new WithLocationResult(location, Result(document, StatusCodes.Status201Created));

    public static IResult NotFound(string detail) =>
        Error(new JsonApiError { StatusCode = StatusCodes.Status404NotFound, Title = "Not found", Detail = detail });

    /// <summary>422 Unprocessable Entity.</summary>
    public static IResult Validation(string detail) =>
        Error(new JsonApiError { StatusCode = StatusCodes.Status422UnprocessableEntity, Title = "Validation failed", Detail = detail });

    public static IResult BadRequest(string title, string detail) =>
        Error(new JsonApiError { StatusCode = StatusCodes.Status400BadRequest, Title = title, Detail = detail });

    /// <summary>403 Forbidden — the spec's required refusal for unsupported writes, e.g. a
    /// relationship update the server does not allow
    /// (https://jsonapi.org/format/#crud-updating-relationship-responses-403).</summary>
    public static IResult Forbidden(string detail) =>
        Error(new JsonApiError { StatusCode = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = detail });

    /// <summary>409 Conflict — e.g. a request document whose type does not match the endpoint
    /// (https://jsonapi.org/format/#crud-updating-responses-409).</summary>
    public static IResult Conflict(string detail) =>
        Error(new JsonApiError { StatusCode = StatusCodes.Status409Conflict, Title = "Conflict", Detail = detail });

    /// <summary>Errors render as RFC 7807 problem details (application/problem+json), consistent
    /// with the framework-generated errors from UseExceptionHandler/UseStatusCodePages.</summary>
    public static IResult Error(JsonApiError error) =>
        Results.Problem(statusCode: error.StatusCode, title: error.Title, detail: error.Detail);
}
