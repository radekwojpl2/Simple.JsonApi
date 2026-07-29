using System.Text.Json.Nodes;

namespace JsonApiLite.OpenApi.Tests;

/// <summary>The document links member. The set varies by document kind rather than being uniform,
/// because the specification places pagination on collections and <c>related</c> on relationships —
/// describing them everywhere would claim members the endpoint cannot send.</summary>
public sealed class LinksSchemaTests
{
    private static IReadOnlyCollection<string> LinkNames(JsonObject document)
    {
        var links = document.Property("links");
        Assert.NotNull(links);
        return links.PropertyNames();
    }

    [Fact]
    public void A_single_resource_document_describes_self_only()
    {
        var names = LinkNames(SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>());

        Assert.Equal(["self"], names);
    }

    /// <summary>Spec: "Pagination links MUST appear in the links object that corresponds to a
    /// collection."</summary>
    [Fact]
    public void A_collection_document_describes_self_and_the_pagination_links()
    {
        var names = LinkNames(
            SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>());

        Assert.Contains("self", names);
        Assert.Contains("first", names);
        Assert.Contains("prev", names);
        Assert.Contains("next", names);
        Assert.Contains("last", names);
        Assert.Equal(5, names.Count);
    }

    /// <summary>Spec: "related: a related resource link when primary data represents a
    /// relationship."</summary>
    [Fact]
    public void A_to_one_linkage_document_describes_self_and_related()
    {
        var names = LinkNames(SchemaFixture.Response<ToOneLinkageDocument>());

        Assert.Contains("self", names);
        Assert.Contains("related", names);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void A_to_many_linkage_document_describes_related_and_pagination()
    {
        var names = LinkNames(SchemaFixture.Response<ToManyLinkageDocument>());

        Assert.Contains("self", names);
        Assert.Contains("related", names);
        Assert.Contains("next", names);
        Assert.Equal(6, names.Count);
    }

    [Fact]
    public void An_error_document_describes_self_only()
    {
        var names = LinkNames(SchemaFixture.Response<ErrorDocument>(500));

        Assert.Equal(["self"], names);
    }

    [Fact]
    public void Pagination_is_absent_from_kinds_whose_primary_data_is_not_a_collection()
    {
        foreach (var names in new[]
        {
            LinkNames(SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>()),
            LinkNames(SchemaFixture.Response<ToOneLinkageDocument>()),
            LinkNames(SchemaFixture.Response<ErrorDocument>(500)),
        })
        {
            Assert.DoesNotContain("first", names);
            Assert.DoesNotContain("prev", names);
            Assert.DoesNotContain("next", names);
            Assert.DoesNotContain("last", names);
        }
    }

    [Fact]
    public void Related_is_absent_from_kinds_whose_primary_data_is_not_a_relationship()
    {
        Assert.DoesNotContain("related",
            LinkNames(SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>()));
        Assert.DoesNotContain("related",
            LinkNames(SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>()));
        Assert.DoesNotContain("related", LinkNames(SchemaFixture.Response<ErrorDocument>(500)));
    }

    /// <summary>Spec: a link is "a string whose value is a URI-reference pointing to the link's
    /// target, a link object or null if the link does not exist."</summary>
    [Fact]
    public void A_link_accepts_either_a_url_or_an_object_carrying_a_url_and_metadata()
    {
        var self = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships>>()
            .Property("links")?.Property("self");

        Assert.NotNull(self);
        var options = self["anyOf"]?.AsArray();
        Assert.NotNull(options);
        Assert.Equal(2, options.Count);

        var bare = options[0]!.AsObject();
        Assert.Equal("string", bare["type"]?.GetValue<string>());
        Assert.Equal("uri", bare["format"]?.GetValue<string>());

        var carrying = options[1]!.AsObject();
        Assert.Equal("object", carrying["type"]?.GetValue<string>());
        Assert.Equal("uri", carrying.Property("href")?["format"]?.GetValue<string>());
        Assert.Equal("object", carrying.Property("meta")?["type"]?.GetValue<string>());
        Assert.Equal(["href"], carrying.RequiredNames());
    }

    /// <summary>FR-008a. The library's links object has no such member, so no endpoint built on it can
    /// send one, and describing it would be a claim no test could ever falsify: every envelope member
    /// is optional, so an absent describedby validates forever.</summary>
    [Theory]
    [InlineData("resource")]
    [InlineData("collection")]
    [InlineData("to-one")]
    [InlineData("to-many")]
    [InlineData("errors")]
    public void No_document_kind_describes_describedby(string kind)
    {
        JsonObject schema = kind switch
        {
            "resource" => SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>(),
            "collection" => SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>(),
            "to-one" => SchemaFixture.Response<ToOneLinkageDocument>(),
            "to-many" => SchemaFixture.Response<ToManyLinkageDocument>(),
            _ => SchemaFixture.Response<ErrorDocument>(500),
        };

        Assert.False(schema.MentionsMember("describedby"));
    }
}
