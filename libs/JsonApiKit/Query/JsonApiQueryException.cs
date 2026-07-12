namespace JsonApiKit;

/// <summary>Thrown by <see cref="JsonApiQuery"/> parsing for invalid query parameters; translated
/// to a 400 response by <see cref="JsonApiQueryExceptionHandler"/>.</summary>
public sealed class JsonApiQueryException(JsonApiError error) : Exception(error.Detail)
{
    public JsonApiError Error { get; } = error;
}
