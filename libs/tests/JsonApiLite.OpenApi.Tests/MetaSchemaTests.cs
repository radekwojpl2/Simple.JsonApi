namespace JsonApiLite.OpenApi.Tests;

/// <summary>The document metadata member. The specification reserves no member names there, so the
/// shape is always the endpoint's own — which is why it can only come from what the endpoint
/// declared, and why a document declaring nothing must be described as unconstrained rather than as
/// empty.</summary>
public sealed class MetaSchemaTests
{
    [Fact]
    public void A_declared_shape_is_described_with_its_members_named_and_typed()
    {
        var meta = SchemaFixture
            .Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>>()
            .Property("meta");

        Assert.NotNull(meta);
        Assert.Equal("object", meta["type"]?.GetValue<string>());
        Assert.Equal("integer", meta.Property("total")?["type"]?.GetValue<string>());
        Assert.Equal("integer", meta.Property("pageCount")?["type"]?.GetValue<string>());
    }

    /// <summary>The trap this feature had to avoid: <c>Meta</c> holds its wire form in a single
    /// <c>JsonObject Members</c> behind a converter, so reflecting it would publish a <c>members</c>
    /// member that no endpoint ever sends.</summary>
    [Fact]
    public void An_undeclared_shape_is_an_unconstrained_object_and_never_mentions_members()
    {
        var meta = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships>>()
            .Property("meta");

        Assert.NotNull(meta);
        Assert.Equal("object", meta["type"]?.GetValue<string>());
        Assert.Empty(meta.PropertyNames());
    }

    /// <summary>The same trap through the other door: <c>Meta&lt;T&gt;</c> satisfies the document's
    /// <c>TMeta : class, IMeta</c> constraint, so an equality test against <c>Meta</c> would miss it
    /// and describe <c>{ members, value }</c>.</summary>
    [Fact]
    public void A_declared_shape_wrapped_in_Meta_is_still_unconstrained()
    {
        var meta = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, Meta<PageMeta>>>()
            .Property("meta");

        Assert.NotNull(meta);
        Assert.Empty(meta.PropertyNames());
    }

    [Fact]
    public void No_schema_anywhere_describes_the_converter_backed_members_field()
    {
        var schema = SchemaFixture
            .Response<ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>>();

        Assert.False(schema.MentionsMember("members"));
        Assert.False(schema.MentionsMember("value"));
    }

    [Fact]
    public void A_nested_shape_is_described_to_the_same_depth_as_attributes()
    {
        var meta = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, NestedMeta>>()
            .Property("meta");

        Assert.NotNull(meta);
        Assert.Equal("integer", meta.Property("page")?.Property("total")?["type"]?.GetValue<string>());
        Assert.Contains("array", meta.Property("warnings").TypeNames());
        Assert.Equal("string", meta.Property("warnings")?["items"]?["type"]?.GetValue<string>());
        Assert.Contains("object", meta.Property("counts").TypeNames());
        Assert.Equal("integer", meta.Property("counts")?["additionalProperties"]?["type"]?.GetValue<string>());
    }

    /// <summary>Enum members as the app actually writes them — the same rule the attributes walker
    /// applies, so the description and the wire cannot disagree.</summary>
    [Fact]
    public void An_enum_in_a_declared_shape_uses_the_same_convention_as_attributes()
    {
        var order = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, NestedMeta>>()
            .Property("meta")?.Property("order");

        Assert.NotNull(order);
        Assert.NotNull(order["enum"]);
    }

    /// <summary>Terminates rather than recursing forever, exactly as a self-referencing attributes
    /// type already does.</summary>
    [Fact]
    public void A_self_referencing_shape_terminates()
    {
        var meta = SchemaFixture
            .Response<ResourceDocument<ContactAttributes, ContactRelationships, RecursiveMeta>>()
            .Property("meta");

        Assert.NotNull(meta);
        Assert.Equal("integer", meta.Property("depth")?["type"]?.GetValue<string>());
        // Described as a bare object rather than walked again, which is how the recursion stops.
        Assert.Contains("object", meta.Property("parent").TypeNames());
        Assert.Empty(meta.Property("parent")?.PropertyNames() ?? []);
    }

    /// <summary>An error document's metadata cannot be declared — the document form is non-generic —
    /// so it is unconstrained by necessity rather than by choice.</summary>
    [Fact]
    public void An_error_document_describes_unconstrained_metadata()
    {
        var meta = SchemaFixture.Response<ErrorDocument>(500).Property("meta");

        Assert.NotNull(meta);
        Assert.Equal("object", meta["type"]?.GetValue<string>());
        Assert.Empty(meta.PropertyNames());
    }
}
