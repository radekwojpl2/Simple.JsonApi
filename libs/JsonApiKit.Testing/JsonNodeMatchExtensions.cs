using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiKit.Testing;

/// <summary>Object-shaped assertions over JSON:API response JSON.</summary>
public static class JsonNodeMatchExtensions
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Asserts that <paramref name="actual"/> contains everything in
    /// <paramref name="expected"/> (typically an anonymous object, serialized with web/camelCase
    /// conventions). Objects are compared as subsets — members the server returns beyond the
    /// expected ones are ignored — so tests state exactly the shape they care about:
    /// <code>document["data"].ShouldMatch(new { type = "contacts", attributes = new { firstName = "Jan" } });</code>
    /// Arrays must match element by element; scalars must be equal. Throws
    /// <see cref="JsonApiMatchException"/> with the mismatch path and both payloads otherwise.</summary>
    public static void ShouldMatch(this JsonNode? actual, object? expected)
    {
        var expectedNode = JsonSerializer.SerializeToNode(expected, Web);
        if (FirstMismatch(expectedNode, actual, "$", strict: false) is { } mismatch)
        {
            throw new JsonApiMatchException(
                $"JSON mismatch at {mismatch}\n\nExpected (subset):\n{Pretty(expectedNode)}\n\nActual:\n{Pretty(actual)}");
        }
    }

    /// <summary>Asserts that <paramref name="actual"/> is exactly <paramref name="expected"/>:
    /// like <see cref="ShouldMatch"/>, but an object member the server returns that the expectation
    /// does not declare is a mismatch too, so the assertion covers the entire payload. Object
    /// member order stays insignificant; array order and count stay exact. Pass an anonymous
    /// object or a prebuilt <see cref="JsonNode"/>.</summary>
    public static void ShouldMatchExactly(this JsonNode? actual, object? expected)
    {
        var expectedNode = expected as JsonNode ?? JsonSerializer.SerializeToNode(expected, Web);
        if (FirstMismatch(expectedNode, actual, "$", strict: true) is { } mismatch)
        {
            throw new JsonApiMatchException(
                $"JSON mismatch at {mismatch}\n\nExpected (exact):\n{Pretty(expectedNode)}\n\nActual:\n{Pretty(actual)}");
        }
    }

    private static string? FirstMismatch(JsonNode? expected, JsonNode? actual, string path, bool strict)
    {
        switch (expected)
        {
            case JsonObject expectedObject:
                if (actual is not JsonObject actualObject)
                {
                    return $"{path}: expected an object but got {Describe(actual)}";
                }
                foreach (var (name, expectedValue) in expectedObject)
                {
                    if (!actualObject.TryGetPropertyValue(name, out var actualValue))
                    {
                        return $"{path}.{name}: member is missing";
                    }
                    if (FirstMismatch(expectedValue, actualValue, $"{path}.{name}", strict) is { } mismatch)
                    {
                        return mismatch;
                    }
                }
                if (strict)
                {
                    var unexpected = actualObject
                        .Where(member => !expectedObject.ContainsKey(member.Key))
                        .Select(member => member.Key)
                        .ToList();
                    if (unexpected.Count > 0)
                    {
                        return $"{path}: unexpected members: {string.Join(", ", unexpected)}";
                    }
                }
                return null;

            case JsonArray expectedArray:
                if (actual is not JsonArray actualArray)
                {
                    return $"{path}: expected an array but got {Describe(actual)}";
                }
                if (expectedArray.Count != actualArray.Count)
                {
                    return $"{path}: expected {expectedArray.Count} elements but got {actualArray.Count}";
                }
                for (var i = 0; i < expectedArray.Count; i++)
                {
                    if (FirstMismatch(expectedArray[i], actualArray[i], $"{path}[{i}]", strict) is { } mismatch)
                    {
                        return mismatch;
                    }
                }
                return null;

            default:
                return JsonNode.DeepEquals(expected, actual)
                    ? null
                    : $"{path}: expected {Describe(expected)} but got {Describe(actual)}";
        }
    }

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "null";

    private static string Pretty(JsonNode? node) => node?.ToJsonString(Indented) ?? "null";
}
