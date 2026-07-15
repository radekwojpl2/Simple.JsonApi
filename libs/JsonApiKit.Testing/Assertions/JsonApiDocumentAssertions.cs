using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiKit.Testing;

/// <summary>Spec-level assertions over JSON:API documents, for conformance tests. Like
/// <see cref="JsonNodeAssertions"/>, failures throw <see cref="JsonApiMatchException"/>
/// rather than depending on any test framework.</summary>
public static class JsonApiDocumentAssertions
{
    /// <summary>Asserts the document's data array is ordered by attribute <paramref name="field"/>
    /// (https://jsonapi.org/format/#fetching-sorting) — numerically when the attribute is a JSON
    /// number, ordinally when it is a string (matching a database's byte-wise collation for ASCII
    /// test data). Requires at least two rows, since fewer cannot prove an order.</summary>
    public static void ShouldBeSortedBy(this JsonNode? document, string field, bool descending = false)
    {
        var values = document![JsonApiMember.Data]!.AsArray()
            .Select(resource => resource![JsonApiMember.Attributes]![field]!.AsValue())
            .Select(value => value.GetValueKind() == JsonValueKind.Number
                ? (IComparable)value.GetValue<decimal>()
                : value.GetValue<string>())
            .ToList();
        if (values.Count < 2)
        {
            throw new JsonApiMatchException("Sorting needs at least two rows to prove an order.");
        }

        for (var i = 1; i < values.Count; i++)
        {
            var comparison = CompareOrdinal(values[i - 1], values[i]);
            if (descending ? comparison < 0 : comparison > 0)
            {
                var direction = descending ? "descending" : "ascending";
                throw new JsonApiMatchException(
                    $"data is not sorted {direction} by '{field}': '{values[i - 1]}' precedes " +
                    $"'{values[i]}' at index {i}.\n\nValues: [{string.Join(", ", values)}]");
            }
        }
    }

    /// <summary>Asserts the document's top-level links object carries a non-null
    /// <paramref name="key"/> (https://jsonapi.org/format/#fetching-pagination).</summary>
    public static void ShouldHaveAvailableLink(this JsonNode? document, string key)
    {
        var links = document![JsonApiMember.Links]!.AsObject();
        if (!links.TryGetPropertyValue(key, out var value) || value is null)
        {
            var actual = links.ContainsKey(key) ? "an explicit null" : "no such member";
            throw new JsonApiMatchException($"links.{key} should be available, but the document carries {actual}.");
        }
    }

    /// <summary>Asserts link <paramref name="key"/> is unavailable — the spec allows either
    /// omission or an explicit null value (https://jsonapi.org/format/#fetching-pagination).</summary>
    public static void ShouldHaveUnavailableLink(this JsonNode? document, string key)
    {
        var links = document![JsonApiMember.Links]!.AsObject();
        if (links.TryGetPropertyValue(key, out var value) && value is not null)
        {
            throw new JsonApiMatchException(
                $"links.{key} should be unavailable (omitted or null), but is {value.ToJsonString()}.");
        }
    }

    /// <summary>Strings compare ordinally; other comparables use their natural order.</summary>
    private static int CompareOrdinal(IComparable left, IComparable right)
    {
        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }
        return left.CompareTo(right);
    }
}
