namespace JsonApiLite.OpenApi.Tests;

/// <summary>A request body is the caller's document. It has no links to follow, nothing sideloaded,
/// and no server-computed metadata, so describing those members would tell a client to send what it
/// must not. This is the edge case a phase adding an envelope member is most likely to break by
/// accident, which is why it is pinned separately rather than left to the response tests.</summary>
public sealed class RequestSchemaTests
{
    [Fact]
    public void A_create_request_describes_data_only()
    {
        var schema = SchemaFixture.Request<ResourceDocument<ContactAttributes, ContactRelationships>>();

        Assert.Equal(["data"], schema.PropertyNames());
    }

    [Fact]
    public void A_patch_request_describes_data_only()
    {
        var schema = SchemaFixture.Request<ResourceDocument<ContactAttributes, ContactRelationships>>(includeId: true);

        Assert.Equal(["data"], schema.PropertyNames());
    }

    /// <summary>The case that matters most: the document type carries a declared metadata shape and a
    /// declared sideload shape, and neither may reach the request schema.</summary>
    [Fact]
    public void A_request_whose_type_declares_meta_and_included_describes_neither()
    {
        var schema = SchemaFixture
            .Request<ResourceDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>();

        Assert.Equal(["data"], schema.PropertyNames());
    }

    [Fact]
    public void A_linkage_request_describes_data_only()
    {
        Assert.Equal(["data"], SchemaFixture.Request<ToOneLinkageDocument>().PropertyNames());
        Assert.Equal(["data"], SchemaFixture.Request<ToManyLinkageDocument>().PropertyNames());
    }

    [Theory]
    [InlineData("links")]
    [InlineData("meta")]
    [InlineData("included")]
    public void No_request_schema_mentions_an_envelope_member_at_the_document_level(string member)
    {
        var schema = SchemaFixture
            .Request<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>();

        Assert.DoesNotContain(member, schema.PropertyNames());
    }
}
