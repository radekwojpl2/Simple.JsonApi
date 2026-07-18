namespace JsonApiLite;

/// <summary>Where in the request the error originates: a JSON pointer into the document, or a
/// query parameter name.</summary>
public sealed record ErrorSource
{
    public string? Pointer { get; init; }

    public string? Parameter { get; init; }
}
