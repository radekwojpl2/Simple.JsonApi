namespace JsonApiLite.OpenApi.Tests;

/// <summary>The sideload member, described from the types the author declared on the document. One
/// flat array whatever the declaration — the per-type members are a reading convenience in C#, not a
/// wire shape.</summary>
public sealed class IncludedSchemaTests
{
    private static IReadOnlyList<string> DeclaredResourceTypes(string member = "included", bool collection = false)
    {
        var schema = collection
            ? SchemaFixture.Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>()
            : SchemaFixture.Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>();

        var options = schema.Property(member)?["items"]?["anyOf"]?.AsArray();
        Assert.NotNull(options);

        return [.. options.Select(option =>
            option!["properties"]?["type"]?["const"]?.GetValue<string>() ?? "<none>")];
    }

    /// <summary>Spec: "In a compound document, all included resources MUST be represented as an array
    /// of resource objects in a top-level included member."</summary>
    [Fact]
    public void A_declared_shape_is_described_as_one_array_constrained_to_its_types()
    {
        var included = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>()
            .Property("included");

        Assert.NotNull(included);
        Assert.Equal("array", included["type"]?.GetValue<string>());
        Assert.Equal(["companies", "tags"], DeclaredResourceTypes());
    }

    [Fact]
    public void A_declared_shape_on_a_collection_document_is_described_the_same_way()
    {
        Assert.Equal(["companies", "tags"], DeclaredResourceTypes(collection: true));
    }

    /// <summary>Each entry is a full resource object, not merely an identifier — its attributes and
    /// relationships are described from the declared element type.</summary>
    [Fact]
    public void Each_declared_entry_describes_its_attributes_and_relationships()
    {
        var options = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>()
            .Property("included")?["items"]?["anyOf"]?.AsArray();

        Assert.NotNull(options);
        var companies = options
            .Select(option => option!.AsObject())
            .Single(option => option["properties"]?["type"]?["const"]?.GetValue<string>() == "companies");

        var name = companies["properties"]?["attributes"]?["properties"]?["name"]?.AsObject();
        Assert.Contains("string", name.TypeNames());
        Assert.Contains("id", companies.RequiredNames());
    }

    [Fact]
    public void An_undeclared_shape_is_an_unconstrained_resource_array()
    {
        var included = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships>>()
            .Property("included");

        Assert.NotNull(included);
        Assert.Equal("array", included["type"]?.GetValue<string>());
        Assert.Null(included["items"]?["anyOf"]);
        Assert.Equal("object", included["items"]?["type"]?.GetValue<string>());
    }

    /// <summary>A declaration naming no types at all carries no more information than declaring
    /// nothing, so it must be described identically — never as a list that can hold nothing.</summary>
    [Fact]
    public void A_declaration_naming_no_types_is_described_as_no_declaration_is()
    {
        var empty = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, EmptyIncluded>>()
            .Property("included");
        var undeclared = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships>>()
            .Property("included");

        Assert.Equal(undeclared?.ToJsonString(), empty?.ToJsonString());
    }

    /// <summary>A document may sideload the type it returns as primary data. The two schemas must both
    /// appear and not interfere.</summary>
    [Fact]
    public void A_sideloadable_type_matching_the_primary_data_does_not_collide()
    {
        var schema = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta, SelfIncluded>>();

        Assert.Equal("contacts", schema.Property("data")?.Property("type")?["const"]?.GetValue<string>());
        Assert.Equal(
            ["contacts"],
            schema.Property("included")?["items"]?["anyOf"]?.AsArray()
                .Select(o => o!["properties"]?["type"]?["const"]?.GetValue<string>()).ToList());
    }

    /// <summary>FR-022. Spec: the included member "only appears when the document contains a top-level
    /// data key" — and these kinds have no resource primary data to relate to.</summary>
    [Fact]
    public void Linkage_and_error_documents_never_describe_a_sideload_member()
    {
        Assert.DoesNotContain("included", SchemaFixture.Response<ToOneLinkageDocument>().PropertyNames());
        Assert.DoesNotContain("included", SchemaFixture.Response<ToManyLinkageDocument>().PropertyNames());
        Assert.DoesNotContain("included", SchemaFixture.Response<ErrorDocument>(500).PropertyNames());
    }
}
