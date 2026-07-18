using System.Text.Json.Nodes;

namespace JsonApiLite;

/// <summary>One spec error object; all members are optional, so an endpoint states only what it
/// knows (https://jsonapi.org/format/#error-objects).</summary>
public sealed record Error
{
    /// <summary>A unique identifier for this occurrence of the problem.</summary>
    public string? Id { get; init; }

    /// <summary>The HTTP status code, as a string per the spec.</summary>
    public string? Status { get; init; }

    /// <summary>An application-specific error code.</summary>
    public string? Code { get; init; }

    public string? Title { get; init; }

    public string? Detail { get; init; }

    public ErrorSource? Source { get; init; }

    /// <summary>Error links; <see cref="JsonApiLite.Links.About"/> points at further detail
    /// about this occurrence.</summary>
    public Links? Links { get; init; }

    public JsonObject? Meta { get; init; }
}
