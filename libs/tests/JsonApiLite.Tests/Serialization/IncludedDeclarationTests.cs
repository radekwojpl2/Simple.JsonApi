namespace JsonApiLite.Tests.Serialization;

/// <summary>The public accessor that reports which resource types a sideload shape declares. It
/// exists so tooling describing a document reads the author's one declaration rather than reflecting
/// over the shape a second time under its own rules.</summary>
public sealed class IncludedDeclarationTests
{
    private sealed record CompanyAttributes(string? Name = null) : IResourceType
    {
        public static string ResourceType => "companies";
    }

    private sealed record CompanyRelationships : IRelationships;

    private sealed record TagAttributes(string? Label = null) : IResourceType
    {
        public static string ResourceType => "tags";
    }

    private sealed record TagRelationships : IRelationships;

    private sealed record Declared : IIncluded
    {
        public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }
        public IReadOnlyList<Resource<TagAttributes, TagRelationships>>? Tags { get; init; }
    }

    private sealed record DeclaresNothing : IIncluded;

    [Fact]
    public void Reports_each_declared_resource_type_by_its_wire_name()
    {
        var declared = IncludedDeclaration.Of(typeof(Declared));

        Assert.Equal(["companies", "tags"], declared.Select(entry => entry.ResourceType));
    }

    /// <summary>The name comes from the element type's own <see cref="IResourceType"/> declaration, so
    /// it cannot drift from the name the resource actually serializes as.</summary>
    [Fact]
    public void Reports_the_element_type_each_member_holds()
    {
        var declared = IncludedDeclaration.Of(typeof(Declared));

        Assert.Equal(
            typeof(Resource<CompanyAttributes, CompanyRelationships>),
            declared.Single(entry => entry.ResourceType == "companies").ElementType);
    }

    /// <summary>Declaration order, which is fixed at the shape rather than left to reflection, so a
    /// document a tool describes twice is described the same way twice.</summary>
    [Fact]
    public void Reports_in_declaration_order()
    {
        var first = IncludedDeclaration.Of(typeof(Declared));
        var again = IncludedDeclaration.Of(typeof(Declared));

        Assert.Equal(first.Select(entry => entry.ResourceType), again.Select(entry => entry.ResourceType));
    }

    [Fact]
    public void A_shape_declaring_nothing_reports_nothing()
    {
        Assert.Empty(IncludedDeclaration.Of(typeof(DeclaresNothing)));
    }

    /// <summary>The untyped default declares nothing, so it says no more than a document that declares
    /// nothing at all — and a caller must not have to special-case it.</summary>
    [Fact]
    public void The_untyped_default_reports_nothing()
    {
        Assert.Empty(IncludedDeclaration.Of(typeof(AnyIncluded)));
    }

    [Fact]
    public void A_type_that_is_not_a_sideload_shape_is_rejected()
    {
        var thrown = Assert.Throws<ArgumentException>(() => IncludedDeclaration.Of(typeof(string)));

        Assert.Contains("IIncluded", thrown.Message);
    }
}
