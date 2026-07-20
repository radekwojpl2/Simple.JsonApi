using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>The spec-fidelity fixes: links-only relationships, link objects, full error objects,
/// free-form meta, registered included types, and correct output under foreign options.</summary>
public class SpecComplianceTests
{
    [Fact]
    public void A_links_only_relationship_reads_through_the_dictionary_flavor()
    {
        var document = Wire.Roundtrip(ContactWithLinksOnlyCompany());

        var company = Assert.IsType<LinksRelationship>(document.Data!.Relationships!["company"]);
        Assert.Equal("/contacts/1/company", company.Links!.Related!.Href);
    }

    [Fact]
    public void A_typed_member_still_requires_data()
    {
        // The same wire document that the dictionary flavor accepts is rejected where a typed
        // data-bearing member is declared.
        var wire = JsonApiSerializer.Serialize(ContactWithLinksOnlyCompany());

        var exception = Assert.ThrowsAny<JsonException>(() =>
            JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(wire));

        Assert.Contains("'data'", exception.Message);
    }

    [Fact]
    public void A_links_only_relationship_writes_without_a_data_member()
    {
        var document = new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new Dictionary<string, Relationship>
                {
                    ["company"] = new LinksRelationship
                    {
                        Links = new Links { Related = "/contacts/1/company" },
                    },
                },
            },
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"links":{"related":"/contacts/1/company"}}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Link_objects_with_meta_round_trip()
    {
        var document = new ResourceCollectionDocument<ContactAttributes>
        {
            Data = [],
            Links = new Links
            {
                Self = new Link("/contacts") { Meta = new Meta<CountMeta>(new CountMeta(10)) },
                Next = "/contacts?page[number]=2",
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"data":[],"links":{"self":{"href":"/contacts","meta":{"count":10}},"next":"/contacts?page[number]=2"}}""",
            json);

        var reread = JsonApiSerializer.Deserialize<ResourceCollectionDocument<ContactAttributes>>(json)!;
        Assert.Equal("/contacts", reread.Links!.Self!.Href);
        Assert.Equal(new CountMeta(10), reread.Links.Self.Meta!.As<CountMeta>());
        Assert.Equal("/contacts?page[number]=2", reread.Links.Next!.Href);
    }

    [Fact]
    public void Error_objects_carry_the_full_spec_surface()
    {
        var document = new ErrorDocument
        {
            Errors =
            [
                new Error
                {
                    Id = "e1",
                    Status = "422",
                    Code = "validation",
                    Title = "Validation failed",
                    Detail = "The title attribute is required.",
                    Source = new ErrorSource { Pointer = "/data/attributes/title" },
                    Links = new Links { About = "/errors/validation" },
                    Meta = new Meta<AttemptMeta>(new AttemptMeta(2)),
                },
            ],
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"errors":[{"id":"e1","status":"422","code":"validation","title":"Validation failed","detail":"The title attribute is required.","source":{"pointer":"/data/attributes/title"},"links":{"about":"/errors/validation"},"meta":{"attempt":2}}]}""",
            json);

        var reread = JsonApiSerializer.Deserialize<ErrorDocument>(json)!;
        var error = Assert.Single(reread.Errors);
        Assert.Equal("validation", error.Code);
        Assert.Equal("/errors/validation", error.Links!.About!.Href);
        Assert.Equal(new AttemptMeta(2), error.Meta!.As<AttemptMeta>());
    }

    [Fact]
    public void Meta_carries_whatever_members_the_endpoint_sent()
    {
        var document = new ResourceCollectionDocument<ContactAttributes>
        {
            Data = [],
            Meta = new Meta
            {
                Members = new JsonObject
                {
                    ["total"] = 42,
                    ["pageCount"] = 5,
                    ["generatedAt"] = "2026-07-18",
                },
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"data":[],"meta":{"total":42,"pageCount":5,"generatedAt":"2026-07-18"}}""",
            json);

        var reread = JsonApiSerializer.Deserialize<ResourceCollectionDocument<ContactAttributes>>(json)!;
        Assert.Equal(42, (int)reread.Meta!.Members["total"]!);
        Assert.Equal("2026-07-18", (string)reread.Meta.Members["generatedAt"]!);
    }

    [Fact]
    public void Meta_reads_back_as_a_declared_shape()
    {
        var json = JsonApiSerializer.Serialize(new ResourceCollectionDocument<ContactAttributes>
        {
            Data = [],
            Meta = new Meta<PageMeta>(new PageMeta(Total: 42, PageCount: 5, GeneratedAt: "2026-07-18")),
        });

        Assert.Equal("""{"data":[],"meta":{"total":42,"pageCount":5,"generatedAt":"2026-07-18"}}""", json);

        var reread = JsonApiSerializer.Deserialize<ResourceCollectionDocument<ContactAttributes>>(json)!;
        Assert.Equal(
            new PageMeta(Total: 42, PageCount: 5, GeneratedAt: "2026-07-18"),
            reread.Meta!.As<PageMeta>());
    }

    [Fact]
    public void Registered_types_make_included_strongly_typed()
    {
        var wire = JsonApiSerializer.Serialize(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships> { Type = ContactAttributes.ResourceType, Id = "1" },
            Included =
            [
                new Resource<CompanyAttributes, CompanyRelationships>
                {
                    Type = CompanyAttributes.ResourceType,
                    Id = "7",
                    Attributes = new CompanyAttributes("Acme"),
                    Relationships = new CompanyRelationships
                    {
                        Owner = Relationship.ToOne<UserAttributes>("9") with { Meta = new Meta<RoleMeta>(new RoleMeta("primary")) },
                    },
                    Links = new Links { Self = "/companies/7" },
                },
                new Resource<TagAttributes> { Type = TagAttributes.ResourceType, Id = "3", Attributes = new TagAttributes("vip") },
            ],
        });

        var options = JsonApiSerializer.CreateOptions(new ResourceTypeRegistry()
            .Map<CompanyAttributes, CompanyRelationships>());
        var document = JsonApiSerializer
            .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(wire, options)!;

        var company = Assert.IsType<Resource<CompanyAttributes, CompanyRelationships>>(
            document.Included![0]);
        Assert.Equal("Acme", company.Attributes!.Name);
        Assert.Equal(new ResourceIdentifier("users", "9"), company.Relationships!.Owner!.Data);
        Assert.Equal("/companies/7", company.Links!.Self!.Href);

        var tag = Assert.IsType<Resource<JsonObject>>(document.Included[1]);
        Assert.Equal("vip", (string)tag.Attributes!["label"]!);
    }

    [Fact]
    public void Included_resources_are_not_lost_under_foreign_serializer_options()
    {
        var document = new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes> { Type = ContactAttributes.ResourceType, Id = "1" },
            Included =
            [
                new Resource<CompanyAttributes>
                {
                    Type = CompanyAttributes.ResourceType,
                    Id = "7",
                    Attributes = new CompanyAttributes("Acme"),
                },
            ],
        };

        var json = JsonSerializer.Serialize(document);

        Assert.Contains("Acme", json);
        Assert.Contains("Attributes", json);
    }

    private static ResourceDocument<ContactAttributes> ContactWithLinksOnlyCompany() =>
        new()
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new Dictionary<string, Relationship>
                {
                    ["company"] = new LinksRelationship
                    {
                        Links = new Links { Related = "/contacts/1/company" },
                    },
                },
            },
        };
}
