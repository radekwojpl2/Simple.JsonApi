namespace JsonApiKit;

/// <summary>Points at the request part that caused the error: a query parameter or a JSON pointer into the body.</summary>
public sealed record JsonApiErrorSource(string? Parameter = null, string? Pointer = null);
