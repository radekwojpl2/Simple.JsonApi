using System.Text.Json.Serialization;

namespace JsonApiKit;

/// <summary>Spec error object (https://jsonapi.org/format/#error-objects). The wire format's
/// <c>status</c> is a string; <see cref="StatusCode"/> carries the numeric value for HTTP responses.</summary>
public sealed record JsonApiError
{
    [JsonIgnore]
    public int StatusCode { get; init; } = 400;

    public string Status => StatusCode.ToString();

    public string? Title { get; init; }

    public string? Detail { get; init; }

    public JsonApiErrorSource? Source { get; init; }
}
