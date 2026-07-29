using System.Text.Json.Nodes;

namespace JsonApiLite.OpenApi.Tests;

/// <summary>Every envelope member is optional on the wire, so describing one as required would make a
/// valid response fail validation. This walks the whole emitted tree rather than the top level,
/// because a required marker added to a nested link object would be just as wrong and much easier to
/// miss.</summary>
public sealed class RequiredMembersTests
{
    /// <summary>The only members anything in a JSON:API document schema may require: primary data,
    /// the errors array, a resource identifier's two halves, and a link object's href.</summary>
    private static readonly HashSet<string> Legitimate = ["data", "errors", "type", "id", "href"];

    public static TheoryData<string, Func<JsonObject>> Documents => new()
    {
        { "resource", () => SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>() },
        { "resource+meta", () => SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships, PageMeta>>() },
        { "resource+included", () => SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>() },
        { "collection", () => SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>() },
        { "collection+meta", () => SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>>() },
        { "collection+included", () => SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>() },
        { "to-one linkage", () => SchemaFixture.Response<ToOneLinkageDocument>() },
        { "to-many linkage", () => SchemaFixture.Response<ToManyLinkageDocument>() },
        { "errors", () => SchemaFixture.Response<ErrorDocument>(500) },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void Nothing_beyond_the_wire_required_members_is_ever_required(string kind, Func<JsonObject> build)
    {
        var schema = build();

        foreach (var required in schema.AllRequired())
        {
            var illegitimate = required.Where(name => !Legitimate.Contains(name)).ToList();
            Assert.Empty(illegitimate);
        }

        Assert.NotEmpty(kind);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void No_envelope_member_is_required_at_the_document_level(string kind, Func<JsonObject> build)
    {
        var required = build().RequiredNames();

        Assert.DoesNotContain("links", required);
        Assert.DoesNotContain("meta", required);
        Assert.DoesNotContain("included", required);
        Assert.NotEmpty(kind);
    }
}
