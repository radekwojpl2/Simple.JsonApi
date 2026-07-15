using System.Text.Json.Nodes;
using JsonApiKit.Testing;

namespace JsonApiKit.Tests;

public class JsonNodeMatchTests
{
    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    [Fact]
    public void ShouldMatch_IgnoresUndeclaredMembers()
    {
        var actual = Parse("""{ "a": 1, "b": 2 }""");

        actual.ShouldMatch(new { a = 1 }); // does not throw
    }

    [Fact]
    public void ShouldMatchExactly_AcceptsIdenticalDocuments()
    {
        var actual = Parse("""{ "a": 1, "b": { "c": [1, 2] } }""");

        actual.ShouldMatchExactly(new { a = 1, b = new { c = new[] { 1, 2 } } });
    }

    [Fact]
    public void ShouldMatchExactly_ObjectMemberOrderIsInsignificant()
    {
        var actual = Parse("""{ "b": 2, "a": 1 }""");

        actual.ShouldMatchExactly(new { a = 1, b = 2 });
    }

    [Fact]
    public void ShouldMatchExactly_ReportsUnexpectedMembersWithPath()
    {
        var actual = Parse("""{ "data": { "type": "widgets", "id": "1", "extra": true, "more": 0 } }""");

        var ex = Assert.Throws<JsonApiMatchException>(() =>
            actual.ShouldMatchExactly(new { data = new { type = "widgets", id = "1" } }));

        Assert.Contains("$.data: unexpected members: extra, more", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_ReportsUnexpectedMembersInNestedArrays()
    {
        var actual = Parse("""{ "data": [ { "type": "widgets", "attributes": { "name": "x" } } ] }""");

        var ex = Assert.Throws<JsonApiMatchException>(() =>
            actual.ShouldMatchExactly(new { data = new[] { new { type = "widgets" } } }));

        Assert.Contains("$.data[0]: unexpected members: attributes", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_StillReportsMissingMembers()
    {
        var actual = Parse("""{ "a": 1 }""");

        var ex = Assert.Throws<JsonApiMatchException>(() => actual.ShouldMatchExactly(new { a = 1, b = 2 }));

        Assert.Contains("$.b: member is missing", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_DistinguishesNullMemberFromMissingMember()
    {
        // "data": null is a legitimate JSON:API payload (empty to-one); it must satisfy an
        // expected null and an absent member must not.
        Parse("""{ "data": null }""").ShouldMatchExactly(new { data = (object?)null });

        var ex = Assert.Throws<JsonApiMatchException>(() =>
            Parse("{}").ShouldMatchExactly(new { data = (object?)null }));
        Assert.Contains("$.data: member is missing", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_NullMemberInActualIsUnexpectedWhenUndeclared()
    {
        var actual = Parse("""{ "a": 1, "b": null }""");

        var ex = Assert.Throws<JsonApiMatchException>(() => actual.ShouldMatchExactly(new { a = 1 }));

        Assert.Contains("$: unexpected members: b", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_ArrayCountStillExact()
    {
        var actual = Parse("""{ "data": [1, 2, 3] }""");

        var ex = Assert.Throws<JsonApiMatchException>(() =>
            actual.ShouldMatchExactly(new { data = new[] { 1, 2 } }));

        Assert.Contains("$.data: expected 2 elements but got 3", ex.Message);
    }

    [Fact]
    public void ShouldMatchExactly_AcceptsPrebuiltJsonNodeExpectation()
    {
        var actual = Parse("""{ "a": 1 }""");
        var expected = Parse("""{ "a": 1 }""");

        actual.ShouldMatchExactly(expected); // builder output is a JsonNode, not an anonymous object
    }
}
