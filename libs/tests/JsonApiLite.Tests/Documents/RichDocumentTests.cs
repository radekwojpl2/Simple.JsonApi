using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>Complexity tier 1: single documents that combine every feature at once — mixed
/// attribute value types, several relationships of both arities, links at every level.</summary>
public class RichDocumentTests
{
    [Fact]
    public void Writes_mixed_type_attributes_with_web_json_conventions()
    {
        var document = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Attributes = new DealAttributes(
                    Title: "Big deal",
                    Amount: 125000.5m,
                    Stage: "proposal",
                    CloseDate: new DateOnly(2026, 9, 30)),
                Relationships = new DealRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                    Owner = Relationship.ToOne<UserAttributes>("9"),
                    Contacts = Relationship.ToMany<ContactAttributes>(["1", "2"]),
                },
                Links = new Links { Self = "/deals/42" },
            },
            Links = new Links { Self = "/deals/42" },
        };

        Assert.Equal(
            """{"data":{"type":"deals","id":"42","attributes":{"title":"Big deal","amount":125000.5,"stage":"proposal","closeDate":"2026-09-30"},"relationships":{"company":{"data":{"type":"companies","id":"7"}},"owner":{"data":{"type":"users","id":"9"}},"contacts":{"data":[{"type":"contacts","id":"1"},{"type":"contacts","id":"2"}]}},"links":{"self":"/deals/42"}},"links":{"self":"/deals/42"}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Special_characters_in_attributes_survive_a_round_trip()
    {
        var attributes = new DealAttributes(
            Title: "Zoë's \"mega\" deal \\ path 💼 東京\nsecond line\ttabbed",
            Stage: "prospecting");
        var document = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Attributes = attributes,
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        var reread = JsonApiSerializer.Deserialize<ResourceDocument<DealAttributes, DealRelationships>>(json);

        Assert.Equal(attributes, reread!.Data!.Attributes);
    }

    [Fact]
    public void Reads_mixed_relationship_states_from_one_update_document()
    {
        var document = Wire.Roundtrip(new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Attributes = new DealAttributes(Stage: "negotiation"),
                Relationships = new DealRelationships
                {
                    Owner = Relationship.ToOne<UserAttributes>("12"),
                    Company = Relationship.EmptyToOne(),
                    Contacts = Relationship.ToMany<ContactAttributes>([]),
                },
            },
        });

        var relationships = document.Data!.Relationships!;
        Assert.Equal(new ResourceIdentifier("users", "12"), relationships.Owner!.Data);
        Assert.NotNull(relationships.Company);
        Assert.Null(relationships.Company.Data);
        Assert.NotNull(relationships.Contacts);
        Assert.Empty(relationships.Contacts.Data);
        Assert.Equal("negotiation", document.Data.Attributes!.Stage);
        Assert.Null(document.Data.Attributes.Title);
    }

    [Fact]
    public void A_declared_meta_type_writes_its_own_members()
    {
        var document = new ResourceCollectionDocument<DealAttributes, DealRelationships, PageMeta>
        {
            Data = [],
            Meta = new PageMeta(Total: 2, PageCount: 1, GeneratedAt: "2026-07-20"),
        };

        Assert.Equal(
            """{"data":[],"meta":{"total":2,"pageCount":1,"generatedAt":"2026-07-20"}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void A_declared_meta_type_survives_a_round_trip()
    {
        var meta = new PageMeta(Total: 2, GeneratedAt: "2026-07-20");
        var document = Wire.Roundtrip(new ResourceDocument<DealAttributes, DealRelationships, PageMeta>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
            },
            Meta = meta,
        });

        Assert.Equal(meta, document.Meta);
        Assert.Null(document.Meta!.PageCount);
    }

    [Fact]
    public void Leaving_the_meta_type_unspoken_keeps_the_built_in_one()
    {
        var document = Wire.Roundtrip(new ResourceCollectionDocument<DealAttributes, DealRelationships>
        {
            Data = [],
            Meta = new Meta<PageMeta>(new PageMeta(Total: 2, PageCount: 1)),
        });

        // The wire carries no type name, so meta reads back as the base type: recover the shape.
        Assert.Equal(new PageMeta(Total: 2, PageCount: 1), document.Meta!.As<PageMeta>());
    }

    [Fact]
    public void Relationship_links_survive_a_round_trip_next_to_data()
    {
        var document = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Relationships = new DealRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7") with
                    {
                        Links = new Links
                        {
                            Self = "/deals/42/relationships/company",
                            Related = "/deals/42/company",
                        },
                    },
                },
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        var reread = JsonApiSerializer.Deserialize<ResourceDocument<DealAttributes, DealRelationships>>(json)!;

        var company = reread.Data!.Relationships!.Company!;
        Assert.Equal("/deals/42/relationships/company", company.Links!.Self!.Href);
        Assert.Equal("/deals/42/company", company.Links.Related!.Href);
        Assert.Equal(new ResourceIdentifier("companies", "7"), company.Data);
    }
}
