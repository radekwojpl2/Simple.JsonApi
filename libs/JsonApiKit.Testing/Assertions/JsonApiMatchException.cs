namespace JsonApiKit.Testing;

/// <summary>Thrown by <see cref="JsonNodeAssertions.ShouldMatch"/> when the actual JSON does
/// not contain the expected members. Deliberately not tied to any test framework; every runner
/// reports the message, which carries the mismatch path and both payloads.</summary>
public sealed class JsonApiMatchException(string message) : Exception(message);
