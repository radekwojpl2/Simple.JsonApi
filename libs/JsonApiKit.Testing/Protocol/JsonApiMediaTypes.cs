namespace JsonApiKit.Testing;

/// <summary>Media types a JSON:API server's responses carry.</summary>
public static class JsonApiMediaTypes
{
    /// <summary>The JSON:API media type (https://jsonapi.org/format/#content-negotiation).</summary>
    public const string JsonApi = "application/vnd.api+json";

    /// <summary>RFC 7807 problem details, the common error format for non-spec-strict servers.</summary>
    public const string ProblemDetails = "application/problem+json";
}
