using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>The declared sideload shape: one member per resource type the document may sideload,
/// plus the place undeclared types land. This is what an endpoint author writes.</summary>
public sealed record ContactIncluded : IIncluded
{
    public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }

    public IReadOnlyList<Resource<TagAttributes, TagRelationships>>? Tags { get; init; }
}

public sealed record TagRelationships : IRelationships
{
    public ToOneRelationship? Owner { get; init; }
}

/// <summary>A declaration naming the same type as the document's primary data, for the edge case
/// where a resource sideloads others of its own kind.</summary>
public sealed record ContactSelfIncluded : IIncluded
{
    public IReadOnlyList<Resource<ContactAttributes, ContactRelationships>>? Contacts { get; init; }
}

/// <summary>Sideloaded resources reached by member rather than by cast — the whole of this
/// feature. Covers the three user stories: reading by member, assembling from declared parts, and
/// keeping an undeclared type reachable.</summary>
public class TypedIncludedTests
{
    private const string TwoTypesJson =
        """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"}},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}},{"type":"tags","id":"3","attributes":{"label":"vip"}}]}""";

    private static ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>?
        ReadDeclared(string json) =>
        JsonApiSerializer
            .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>(json);

    // ---- User Story 1: read by member ------------------------------------------------------

    [Fact]
    public void Sideloaded_resources_are_read_by_member_with_no_cast()
    {
        var document = ReadDeclared(TwoTypesJson)!;

        // The feature in one line: no type test, no unwrapping step.
        Assert.Equal("Acme", document.Included?.Companies?[0].Attributes?.Name);
        Assert.Equal("vip", document.Included?.Tags?[0].Attributes?.Label);
    }

    [Fact]
    public void Each_declared_member_holds_only_its_own_resource_type()
    {
        var document = ReadDeclared(TwoTypesJson)!;

        Assert.Equal("7", Assert.Single(document.Included!.Companies!).Id);
        Assert.Equal("3", Assert.Single(document.Included!.Tags!).Id);
    }

    [Fact]
    public void An_absent_sideload_member_is_distinguishable_from_an_empty_one()
    {
        var absent = ReadDeclared(
            """{"data":{"type":"contacts","id":"1"}}""")!;
        var empty = ReadDeclared(
            """{"data":{"type":"contacts","id":"1"},"included":[]}""")!;

        Assert.Null(absent.Included);
        Assert.NotNull(empty.Included);
        Assert.Null(empty.Included!.Companies);
    }

    [Fact]
    public void A_sideloaded_resource_carries_its_own_relationships_and_meta()
    {
        var document = ReadDeclared(
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"},"relationships":{"owner":{"data":{"type":"users","id":"9"}}},"links":{"self":"/companies/7"},"meta":{"count":3}}]}""")!;

        var company = Assert.Single(document.Included!.Companies!);
        Assert.Equal("Acme", company.Attributes!.Name);
        Assert.Equal(new ResourceIdentifier("users", "9"), company.Relationships!.Owner!.Data);
        Assert.Equal("/companies/7", company.Links!.Self!.Href);
        Assert.NotNull(company.Meta);
    }

    [Fact]
    public void A_declaration_needs_no_resource_type_registry()
    {
        // The D2 consequence: a declared document is its own registry, because its members already
        // say which types to expect. Deserialized with the plain options, no registry supplied.
        var document = ReadDeclared(TwoTypesJson)!;

        Assert.IsType<Resource<CompanyAttributes, CompanyRelationships>>(
            Assert.Single(document.Included!.Companies!));
    }

    // ---- User Story 2: assemble from declared parts ------------------------------------------

    [Fact]
    public void A_declared_document_serializes_identically_to_an_undeclared_one()
    {
        var declared = new ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
        {
            Data = Contact(),
            Included = new ContactIncluded { Companies = [Company()], Tags = [Tag()] },
        };
        var undeclared = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = Contact(),
            Included = [Company(), Tag()],
        };

        Assert.Equal(
            JsonApiSerializer.Serialize(undeclared), JsonApiSerializer.Serialize(declared));
        Assert.Equal(TwoTypesJson, JsonApiSerializer.Serialize(declared));
    }

    [Fact]
    public void Unset_declared_members_contribute_nothing()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
        {
            Data = Contact(),
            Included = new ContactIncluded { Companies = [Company()] },
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"}},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}}]}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void A_document_sideloading_nothing_omits_the_member()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
        {
            Data = Contact(),
            Included = new ContactIncluded(),
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void A_declared_document_survives_a_round_trip()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
        {
            Data = Contact(),
            Included = new ContactIncluded { Companies = [Company()], Tags = [Tag()] },
        };

        var reread = Wire.Roundtrip(document);

        Assert.Equal("Acme", reread.Included?.Companies?[0].Attributes?.Name);
        Assert.Equal("vip", reread.Included?.Tags?[0].Attributes?.Label);
        Assert.Equal(JsonApiSerializer.Serialize(document), JsonApiSerializer.Serialize(reread));
    }

    // ---- Undeclared types are dropped ---------------------------------------------------------

    [Fact]
    public void A_type_no_member_declares_is_dropped()
    {
        var document = ReadDeclared(
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}},{"type":"deals","id":"4","attributes":{"title":"Q3"}}]}""")!;

        // The declared type still arrives; the undeclared one is gone, with nowhere to look for it.
        Assert.Equal("Acme", document.Included?.Companies?[0].Attributes?.Name);
        Assert.Null(document.Included!.Tags);
    }

    [Fact]
    public void A_dropped_resource_is_not_written_back()
    {
        // The cost of dropping, stated as a test rather than left to be discovered: a document that
        // is read and written again loses the resources its declaration did not name.
        const string json =
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}},{"type":"deals","id":"4","attributes":{"title":"Q3"}}]}""";

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}}]}""",
            JsonApiSerializer.Serialize(ReadDeclared(json)!));
    }

    [Fact]
    public void A_document_of_only_undeclared_types_reads_as_an_empty_shape()
    {
        var document = ReadDeclared(
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"widgets","id":"9"}]}""");

        // Not an error — the member is present, every declared list simply stays unset.
        Assert.NotNull(document);
        Assert.NotNull(document!.Included);
        Assert.Null(document.Included!.Companies);
        Assert.Null(document.Included!.Tags);
    }

    [Fact]
    public void An_undeclared_type_is_kept_when_the_document_declares_nothing()
    {
        // The escape hatch still holds everything: dropping is a consequence of declaring, not of
        // the sideload member itself.
        var document = JsonApiSerializer
            .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(
                """{"data":{"type":"contacts","id":"1"},"included":[{"type":"deals","id":"4"}]}""")!;

        Assert.Equal("deals", Assert.Single(document.Included!).Type);
    }

    [Fact]
    public void A_document_may_sideload_its_own_resource_type()
    {
        const string json =
            """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"}},"included":[{"type":"contacts","id":"2","attributes":{"firstName":"Grace","lastName":"Hopper"}}]}""";

        var document = JsonApiSerializer
            .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactSelfIncluded>>(json)!;

        Assert.Equal("Ada", document.Data!.Attributes!.FirstName);
        Assert.Equal("Grace", Assert.Single(document.Included!.Contacts!).Attributes!.FirstName);
        Assert.Equal(json, JsonApiSerializer.Serialize(document));
    }

    // ---- The migration surface promised by FR-022 and quickstart.md -------------------------

    [Fact]
    public void Both_documented_migration_forms_compile_and_carry_the_same_resources()
    {
        // The one breaking form is assigning a collection variable; quickstart.md offers exactly
        // these two remedies. Compiling this test is half the assertion.
        var extras = new List<Resource> { Company(), Tag() };

        var spread = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = Contact(),
            Included = [.. extras],
        };
        var constructed = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = Contact(),
            Included = new AnyIncluded(extras),
        };

        Assert.Equal(2, spread.Included!.Count);
        Assert.Equal(
            JsonApiSerializer.Serialize(spread), JsonApiSerializer.Serialize(constructed));
    }

    [Fact]
    public void The_untyped_read_patterns_survive_unchanged()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = Contact(),
            Included = [Company(), Tag()],
        };

        Assert.Equal("7", document.Included![0].Id);
        Assert.Single(document.Included!.OfType<Resource<CompanyAttributes, CompanyRelationships>>());
        IReadOnlyList<Resource> asList = document.Included!;
        Assert.Equal(2, asList.Count);

        var seen = new List<string>();
        foreach (var resource in document.Included!)
        {
            seen.Add(resource.Type);
        }

        Assert.Equal(["companies", "tags"], seen);
    }

    // ---- A declaration that claims one type twice cannot be read back unambiguously ----------

    [Fact]
    public void A_declaration_naming_one_type_twice_is_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes, ContactRelationships, Meta, DuplicateIncluded>>(
                """{"data":{"type":"contacts","id":"1"},"included":[]}"""));

        Assert.Contains("companies", error.Message);
    }

    public sealed record DuplicateIncluded : IIncluded
    {
        public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }
        public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Employers { get; init; }
        public IReadOnlyList<Resource> Undeclared { get; init; } = [];
    }

    private static Resource<ContactAttributes, ContactRelationships> Contact() =>
        Resource.Create<ContactAttributes, ContactRelationships>(
            "1", new ContactAttributes("Ada", "Lovelace"));

    private static Resource<CompanyAttributes, CompanyRelationships> Company() =>
        Resource.Create<CompanyAttributes, CompanyRelationships>("7", new CompanyAttributes("Acme"));

    private static Resource<TagAttributes, TagRelationships> Tag() =>
        Resource.Create<TagAttributes, TagRelationships>("3", new TagAttributes("vip"));
}
