using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

public class TypedRelationshipsTests
{
    private static ResourceDocument<ContactAttributes, ContactRelationships> Parse(string json) =>
        JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json)!;

    [Fact]
    public void Writes_declared_relationships_by_member_name()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Attributes = new ContactAttributes("Ada", "Lovelace"),
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                    Manager = Relationship.EmptyToOne(),
                    Tags = Relationship.ToMany<TagAttributes>(["3", "9"]),
                },
            },
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"},"relationships":{"company":{"data":{"type":"companies","id":"7"}},"manager":{"data":null},"tags":{"data":[{"type":"tags","id":"3"},{"type":"tags","id":"9"}]}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Omits_relationship_members_left_null()
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
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"data":{"type":"companies","id":"7"}}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Reads_the_tri_state_through_typed_members()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                    Manager = Relationship.EmptyToOne(),
                },
            },
        });

        var relationships = document.Data!.Relationships!;
        Assert.Equal(new ResourceIdentifier("companies", "7"), relationships.Company!.Data);
        Assert.NotNull(relationships.Manager);
        Assert.Null(relationships.Manager.Data);
        Assert.Null(relationships.Tags);
    }

    [Fact]
    public void A_missing_relationships_member_reads_as_null()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships> { Type = ContactAttributes.ResourceType, Id = "1" },
        });

        Assert.Null(document.Data!.Relationships);
    }

    [Fact]
    public void Rejects_an_identifier_array_where_a_to_one_is_declared()
    {
        var body = ContactWithRelationship("company", new JsonObject
        {
            ["data"] = new JsonArray(new JsonObject { ["type"] = "companies", ["id"] = "7" }),
        });

        var exception = Assert.ThrowsAny<JsonException>(() => Parse(body.ToJsonString()));
        Assert.Contains("to-one", exception.Message);
    }

    [Fact]
    public void Rejects_a_single_identifier_where_a_to_many_is_declared()
    {
        var body = ContactWithRelationship("tags", new JsonObject
        {
            ["data"] = new JsonObject { ["type"] = "tags", ["id"] = "3" },
        });

        var exception = Assert.ThrowsAny<JsonException>(() => Parse(body.ToJsonString()));
        Assert.Contains("to-many", exception.Message);
    }

    private static JsonObject ContactWithRelationship(string name, JsonObject relationship) =>
        new()
        {
            ["data"] = new JsonObject
            {
                ["type"] = "contacts",
                ["relationships"] = new JsonObject { [name] = relationship },
            },
        };

    [Fact]
    public void A_typed_document_survives_a_round_trip_unchanged()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Attributes = new ContactAttributes("Ada", "Lovelace"),
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                    Manager = Relationship.EmptyToOne(),
                    Tags = Relationship.ToMany<TagAttributes>(["3"]),
                },
                Links = new Links { Self = "/contacts/1" },
            },
            Meta = new Meta<PageMeta>(new PageMeta(Total: 1, PageCount: 1)),
        };

        var json = JsonApiSerializer.Serialize(document);
        var reread = Parse(json);

        Assert.Equal(json, JsonApiSerializer.Serialize(reread));
    }

    [Fact]
    public void Writes_relationship_meta_alongside_data_and_links()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7") with
                    {
                        Links = new Links { Self = "/contacts/1/relationships/company" },
                        Meta = new Meta<RoleMeta>(new RoleMeta("primary")),
                    },
                },
            },
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"links":{"self":"/contacts/1/relationships/company"},"data":{"type":"companies","id":"7"},"meta":{"role":"primary"}}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Relationship_meta_survives_a_round_trip_on_both_arities()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7") with
                    {
                        Meta = new Meta<RoleMeta>(new RoleMeta("primary")),
                    },
                    Tags = Relationship.ToMany<TagAttributes>(["3", "9"]) with
                    {
                        Meta = new Meta<PageMeta>(new PageMeta(Total: 2)),
                    },
                },
            },
        });

        var relationships = document.Data!.Relationships!;
        Assert.Equal(new RoleMeta("primary"), relationships.Company!.Meta!.As<RoleMeta>());
        // Meta declared as a type on the way out reads back as that type.
        Assert.Equal(new PageMeta(Total: 2), relationships.Tags!.Meta!.As<PageMeta>());

        // The linkage itself is untouched by carrying meta.
        Assert.Equal(new ResourceIdentifier("companies", "7"), relationships.Company.Data);
        Assert.Equal(["3", "9"], relationships.Tags.Data.Select(identifier => identifier.Id));
    }

    [Fact]
    public void Relationship_meta_and_link_meta_stay_distinct()
    {
        var document = new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7") with
                    {
                        Links = new Links
                        {
                            Self = new Link("/contacts/1/relationships/company")
                            {
                                Meta = new Meta<OriginMeta>(new OriginMeta("link")),
                            },
                        },
                        Meta = new Meta<OriginMeta>(new OriginMeta("relationship")),
                    },
                },
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"links":{"self":{"href":"/contacts/1/relationships/company","meta":{"belongsTo":"link"}}},"data":{"type":"companies","id":"7"},"meta":{"belongsTo":"relationship"}}}}}""",
            json);

        var company = Parse(json).Data!.Relationships!.Company!;
        Assert.Equal(new OriginMeta("link"), company.Links!.Self!.Meta!.As<OriginMeta>());
        Assert.Equal(new OriginMeta("relationship"), company.Meta!.As<OriginMeta>());
    }

    [Fact]
    public void A_relationship_without_meta_still_writes_none()
    {
        var relationships = Wire.Roundtrip(new ResourceDocument<ContactAttributes, ContactRelationships>
        {
            Data = new Resource<ContactAttributes, ContactRelationships>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new ContactRelationships { Company = Relationship.ToOne<CompanyAttributes>("7") },
            },
        }).Data!.Relationships!;

        Assert.Null(relationships.Company!.Meta);
    }

    [Fact]
    public void A_typed_collection_document_writes_each_resource()
    {
        var document = new ResourceCollectionDocument<ContactAttributes, ContactRelationships>
        {
            Data =
            [
                new Resource<ContactAttributes, ContactRelationships>
                {
                    Type = ContactAttributes.ResourceType,
                    Id = "1",
                    Relationships = new ContactRelationships
                    {
                        Company = Relationship.ToOne<CompanyAttributes>("7"),
                    },
                },
            ],
        };

        Assert.Equal(
            """{"data":[{"type":"contacts","id":"1","relationships":{"company":{"data":{"type":"companies","id":"7"}}}}]}""",
            JsonApiSerializer.Serialize(document));
    }
}
