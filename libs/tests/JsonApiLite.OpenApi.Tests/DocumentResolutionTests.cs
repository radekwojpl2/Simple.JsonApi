namespace JsonApiLite.OpenApi.Tests;

/// <summary>Which document types the annotation understands. The four-argument forms are the ones
/// 002 added and this package rejected, which stopped the sample starting; the rest are the arities
/// that already worked and must keep working.</summary>
public sealed class DocumentResolutionTests
{
    [Fact]
    public void A_resource_document_declaring_its_sideload_shape_is_accepted()
    {
        var schema = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>();

        Assert.Contains("data", schema.PropertyNames());
    }

    [Fact]
    public void A_collection_document_declaring_its_sideload_shape_is_accepted()
    {
        var schema = SchemaFixture
            .Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>>();

        Assert.Equal("array", schema.Property("data")?["type"]?.GetValue<string>());
    }

    /// <summary>FR-002: declaring a sideload shape must not change how the primary data is described.
    /// Compared as serialized JSON, because that is what a consumer reads.</summary>
    [Fact]
    public void Declaring_a_sideload_shape_leaves_the_primary_data_identical()
    {
        var declared = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>()
            .Property("data");
        var undeclared = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships>>()
            .Property("data");

        Assert.Equal(undeclared?.ToJsonString(), declared?.ToJsonString());
    }

    [Fact]
    public void Declaring_a_sideload_shape_leaves_collection_primary_data_identical()
    {
        var declared = SchemaFixture
            .Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>()
            .Property("data");
        var undeclared = SchemaFixture
            .Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships>>()
            .Property("data");

        Assert.Equal(undeclared?.ToJsonString(), declared?.ToJsonString());
    }

    public static TheoryData<Type> AcceptedForms =>
    [
        typeof(ResourceDocument<ContactAttributes>),
        typeof(ResourceDocument<ContactAttributes, ContactRelationships>),
        typeof(ResourceDocument<ContactAttributes, ContactRelationships, PageMeta>),
        typeof(ResourceDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>),
        typeof(ResourceCollectionDocument<ContactAttributes>),
        typeof(ResourceCollectionDocument<ContactAttributes, ContactRelationships>),
        typeof(ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>),
        typeof(ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta, ContactIncluded>),
        typeof(ToOneLinkageDocument),
        typeof(ToManyLinkageDocument),
        typeof(ErrorDocument),
    ];

    /// <summary>Every arity, including the ones that worked before. Resolution moved from an
    /// enumerated list to a walk up the inheritance chain, so this is what says the walk did not lose
    /// a form on the way.</summary>
    [Theory]
    [MemberData(nameof(AcceptedForms))]
    public void Every_document_form_the_library_defines_is_accepted(Type documentType)
    {
        var body = JsonApiBody.Response(documentType, 200);

        Assert.NotNull(body);
    }

    /// <summary>FR-003: widening what is accepted must not be achieved by deleting the check. A type
    /// that is not a JSON:API document must still fail loudly, naming itself and the accepted forms,
    /// rather than yield an empty schema nobody notices.</summary>
    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(ContactAttributes))]
    [InlineData(typeof(Resource<ContactAttributes, ContactRelationships>))]
    [InlineData(typeof(List<ContactAttributes>))]
    public void An_unsupported_type_still_throws_and_names_what_is_accepted(Type notADocument)
    {
        var thrown = Assert.Throws<ArgumentException>(() => JsonApiBody.Response(notADocument, 200));

        Assert.Contains(notADocument.ToString(), thrown.Message);
        Assert.Contains("ResourceDocument<>", thrown.Message);
        Assert.Contains("ErrorDocument", thrown.Message);
    }
}
