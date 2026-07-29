namespace JsonApiLite.OpenApi.Tests;

/// <summary>Characterization tests: what each document kind emits before any envelope member is
/// added. Their job is not to approve today's output but to make the next phase's diff visible — a
/// change here that nobody intended is a regression, and these are what say so.</summary>
public sealed class EnvelopeBaselineTests
{
    [Fact]
    public void A_single_resource_document_describes_data_and_requires_it()
    {
        var schema = SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships>>();

        Assert.Equal(["data"], schema.RequiredNames());
        Assert.Contains("data", schema.PropertyNames());

        var data = schema.Property("data");
        Assert.NotNull(data);
        Assert.Equal("object", data["type"]?.GetValue<string>());
        Assert.Equal("contacts", data.Property("type")?["const"]?.GetValue<string>());
    }

    [Fact]
    public void A_collection_document_describes_data_as_an_array()
    {
        var schema = SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>();

        var data = schema.Property("data");
        Assert.NotNull(data);
        Assert.Equal("array", data["type"]?.GetValue<string>());
        Assert.Equal("contacts", data["items"]?["properties"]?["type"]?["const"]?.GetValue<string>());
    }

    [Fact]
    public void A_to_one_linkage_document_describes_a_nullable_identifier()
    {
        var schema = SchemaFixture.Response<ToOneLinkageDocument>();

        var data = schema.Property("data");
        Assert.NotNull(data);
        Assert.Equal(["type", "id"], data.RequiredNames());
    }

    [Fact]
    public void A_to_many_linkage_document_describes_an_identifier_array()
    {
        var schema = SchemaFixture.Response<ToManyLinkageDocument>();

        var data = schema.Property("data");
        Assert.NotNull(data);
        Assert.Equal("array", data["type"]?.GetValue<string>());
    }

    [Fact]
    public void An_error_document_describes_errors_and_requires_them()
    {
        var schema = SchemaFixture.Response<ErrorDocument>(500);

        Assert.Equal(["errors"], schema.RequiredNames());
        Assert.Equal("array", schema.Property("errors")?["type"]?.GetValue<string>());
    }

    [Fact]
    public void A_request_body_describes_data_only()
    {
        var schema = SchemaFixture.Request<ResourceDocument<ContactAttributes, ContactRelationships>>();

        Assert.Equal(["data"], schema.PropertyNames());
    }

    /// <summary>The one assertion here that was expected to flip. Before this feature the envelope was
    /// a single line and none of these members existed; every other test in this file was written to
    /// keep passing untouched, and did.</summary>
    [Theory]
    [InlineData("links")]
    [InlineData("meta")]
    [InlineData("included")]
    public void Every_envelope_member_is_now_described_on_a_response(string member)
    {
        var collection = SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>>();

        Assert.Contains(member, collection.PropertyNames());
    }
}
