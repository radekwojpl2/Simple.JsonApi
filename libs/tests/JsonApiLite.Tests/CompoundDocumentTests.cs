using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>Complexity tier 2: collections and compound documents — paged lists whose resources
/// carry relationships, heterogeneous included resources, and linkage resolution across the two.</summary>
public class CompoundDocumentTests
{
    private const string PagedCollectionJson =
        """{"data":[{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"},"relationships":{"company":{"data":{"type":"companies","id":"7"}}}},{"type":"contacts","id":"2","attributes":{"firstName":"Grace","lastName":"Hopper"},"relationships":{"company":{"data":{"type":"companies","id":"8"}}}}],"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}},{"type":"companies","id":"8","attributes":{"name":"Globex"}}],"links":{"self":"/contacts?page[number]=2","first":"/contacts?page[number]=1","prev":"/contacts?page[number]=1","next":"/contacts?page[number]=3","last":"/contacts?page[number]=5"},"meta":{"total":42,"pageCount":5}}""";

    [Fact]
    public void Writes_a_paged_collection_with_typed_relationships_and_included()
    {
        Assert.Equal(PagedCollectionJson, JsonApiSerializer.Serialize(PagedCollection()));
    }

    [Fact]
    public void Resolves_relationship_linkage_against_included_resources()
    {
        var document = Wire.Roundtrip(PagedCollection());

        var resolved = new List<(string? Contact, string? Company)>();
        foreach (var contact in document.Data)
        {
            var target = contact.Relationships!.Company!.Data!;
            var company = document.Included!.OfType<Resource<JsonObject>>()
                .Single(resource => resource.Type == target.Type && resource.Id == target.Id);
            resolved.Add((contact.Attributes!.FirstName,
                company.Attributes!.Deserialize<CompanyAttributes>(JsonApiSerializer.Options)!.Name));
        }

        Assert.Equal([("Ada", "Acme"), ("Grace", "Globex")], resolved);
    }

    [Fact]
    public void Included_resources_carry_their_own_relationships()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                },
            },
            Included =
            [
                new Resource<CompanyAttributes, CompanyRelationships>
                {
                    Type = CompanyAttributes.ResourceType,
                    Id = "7",
                    Attributes = new CompanyAttributes("Acme"),
                    Relationships = new CompanyRelationships
                    {
                        Owner = Relationship.ToOne<UserAttributes>("9"),
                    },
                },
            ],
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"data":{"type":"companies","id":"7"}}}},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"},"relationships":{"owner":{"data":{"type":"users","id":"9"}}}}]}""",
            json);

        var reread = JsonApiSerializer
            .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json)!;
        var company = Assert.IsType<Resource<JsonObject>>(Assert.Single(reread.Included!));
        Assert.Equal(new ResourceIdentifier("users", "9"), company.ToOne("owner")!.Data);
        Assert.Equal(new CompanyAttributes("Acme"),
            company.Attributes!.Deserialize<CompanyAttributes>(JsonApiSerializer.Options));
    }

    [Fact]
    public void Resource_and_included_links_survive_the_polymorphic_read()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Links = new Links { Self = "/contacts/1" },
            },
            Included =
            [
                new Resource<CompanyAttributes, CompanyRelationships>
                {
                    Type = CompanyAttributes.ResourceType,
                    Id = "7",
                    Attributes = new CompanyAttributes("Acme"),
                    Links = new Links { Self = "/companies/7" },
                },
            ],
        });

        Assert.Equal("/contacts/1", document.Data!.Links!.Self!.Href);
        var company = Assert.IsType<Resource<JsonObject>>(Assert.Single(document.Included!));
        Assert.Equal("/companies/7", company.Links!.Self!.Href);
    }

    [Fact]
    public void An_empty_page_keeps_its_pagination_shape()
    {
        var document = new ResourceCollectionDocument<ContactAttributes, ContactRelationships>
        {
            Data = [],
            Links = new Links { Self = "/contacts?page[number]=1" },
            Meta = new Meta<PageMeta>(new PageMeta(Total: 0, PageCount: 0)),
        };

        Assert.Equal(
            """{"data":[],"links":{"self":"/contacts?page[number]=1"},"meta":{"total":0,"pageCount":0}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void A_compound_collection_survives_a_round_trip_unchanged()
    {
        var json = JsonApiSerializer.Serialize(PagedCollection());

        Assert.Equal(json, JsonApiSerializer.Serialize(Wire.Roundtrip(PagedCollection())));
    }

    private static ResourceCollectionDocument<ContactAttributes, ContactRelationships> PagedCollection() =>
        new()
        {
            Data =
            [
                Contact("1", "Ada", "Lovelace", companyId: "7"),
                Contact("2", "Grace", "Hopper", companyId: "8"),
            ],
            Included = [Company("7", "Acme"), Company("8", "Globex")],
            Links = new Links
            {
                Self = "/contacts?page[number]=2",
                First = "/contacts?page[number]=1",
                Prev = "/contacts?page[number]=1",
                Next = "/contacts?page[number]=3",
                Last = "/contacts?page[number]=5",
            },
            Meta = new Meta<PageMeta>(new PageMeta(Total: 42, PageCount: 5)),
        };

    private static Resource<ContactAttributes, ContactRelationships> Contact(string id,
        string firstName, string lastName, string companyId) =>
        Resource.Create(id, new ContactAttributes(firstName, lastName), new ContactRelationships
        {
            Company = Relationship.ToOne<CompanyAttributes>(companyId),
        });

    private static Resource<CompanyAttributes> Company(string id, string name) =>
        Resource.Create(id, new CompanyAttributes(name));
}
