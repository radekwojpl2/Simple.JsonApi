using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>Read-side semantics, driven by documents built with the object model: valid documents
/// go over the wire via <see cref="Wire.Roundtrip{TDocument}"/>; protocol violations — shapes the
/// model deliberately cannot express — are built as <see cref="JsonObject"/> trees.</summary>
public class DeserializationTests
{
    [Fact]
    public void Reads_typed_attributes()
    {
        var document = Wire.Roundtrip(Contact(new ContactAttributes("Ada", "Lovelace")));

        Assert.Equal(new ContactAttributes("Ada", "Lovelace"), document.Data!.Attributes);
    }

    [Fact]
    public void A_missing_attributes_member_reads_as_null_attributes()
    {
        var document = Wire.Roundtrip(Contact(attributes: null));

        Assert.Null(document.Data!.Attributes);
    }

    [Fact]
    public void A_create_body_without_an_id_reads_as_null_id()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Attributes = new ContactAttributes("Ada"),
            },
        });

        Assert.Null(document.Data!.Id);
    }

    [Fact]
    public void An_absent_relationship_reads_as_null()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()));

        Assert.Null(document.Data!.ToOne("company"));
        Assert.Null(document.Data.ToMany("tags"));
    }

    [Fact]
    public void A_cleared_to_one_reads_as_present_with_null_data()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["company"] = Relationship.EmptyToOne(),
        }));

        var company = document.Data!.ToOne("company");
        Assert.NotNull(company);
        Assert.Null(company.Data);
    }

    [Fact]
    public void A_to_one_reads_its_identifier()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["company"] = Relationship.ToOne<CompanyAttributes>("7"),
        }));

        Assert.Equal(new ResourceIdentifier("companies", "7"), document.Data!.ToOne("company")!.Data);
    }

    [Fact]
    public void A_to_many_reads_the_complete_member_set()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["tags"] = Relationship.ToMany<TagAttributes>(["3", "9"]),
        }));

        Assert.Equal(
            [new ResourceIdentifier("tags", "3"), new ResourceIdentifier("tags", "9")],
            document.Data!.ToMany("tags")!.Data);
    }

    [Fact]
    public void An_empty_to_many_array_reads_as_an_empty_set()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["tags"] = Relationship.ToMany<TagAttributes>([]),
        }));

        var tags = document.Data!.ToMany("tags");
        Assert.NotNull(tags);
        Assert.Empty(tags.Data);
    }

    [Fact]
    public void ToOne_throws_when_the_document_sent_an_identifier_array()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["company"] = Relationship.ToMany<CompanyAttributes>(["7"]),
        }));

        Assert.Throws<InvalidOperationException>(() => document.Data!.ToOne("company"));
    }

    [Fact]
    public void ToMany_throws_when_the_document_sent_a_single_identifier()
    {
        var document = Wire.Roundtrip(Contact(relationships: new()
        {
            ["tags"] = Relationship.ToOne<TagAttributes>("3"),
        }));

        Assert.Throws<InvalidOperationException>(() => document.Data!.ToMany("tags"));
    }

    [Fact]
    public void A_relationship_with_none_of_data_links_or_meta_is_rejected()
    {
        // The spec requires at least one of the three; meta alone is enough, so the empty object
        // is the only rejectable form.
        var body = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "contacts",
                ["relationships"] = new JsonObject { ["company"] = new JsonObject() },
            },
        };

        var exception = Assert.ThrowsAny<JsonException>(() => Parse(body));
        Assert.Contains("'meta'", exception.Message);
    }

    [Fact]
    public void A_document_without_a_data_member_is_rejected()
    {
        var body = new JsonObject
        {
            ["meta"] = new JsonObject { ["total"] = 1, ["pageCount"] = 1 },
        };

        Assert.ThrowsAny<JsonException>(() => Parse(body));
    }

    [Fact]
    public void A_resource_without_a_type_is_rejected()
    {
        var body = new JsonObject
        {
            ["data"] = new JsonObject { ["id"] = "1" },
        };

        Assert.ThrowsAny<JsonException>(() => Parse(body));
    }

    [Fact]
    public void Unknown_members_are_skipped()
    {
        var body = new JsonObject
        {
            ["jsonapi"] = new JsonObject { ["version"] = "1.1" },
            ["data"] = new JsonObject
            {
                ["type"] = "contacts",
                ["id"] = "1",
                ["relationships"] = new JsonObject
                {
                    ["company"] = new JsonObject
                    {
                        ["meta"] = new JsonObject { ["note"] = "x" },
                        ["data"] = new JsonObject { ["type"] = "companies", ["id"] = "7" },
                    },
                },
            },
        };

        var document = Parse(body);

        Assert.Equal(new ResourceIdentifier("companies", "7"), document.Data!.ToOne("company")!.Data);
    }

    [Fact]
    public void Included_resources_read_as_json_object_attributes_keyed_by_type()
    {
        var document = Wire.Roundtrip(new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes> { Type = ContactAttributes.ResourceType, Id = "1" },
            Included =
            [
                new Resource<CompanyAttributes> { Type = CompanyAttributes.ResourceType, Id = "7", Attributes = new CompanyAttributes("Acme") },
                new Resource<TagAttributes> { Type = TagAttributes.ResourceType, Id = "3", Attributes = new TagAttributes("vip") },
            ],
        });

        var company = Assert.IsType<Resource<JsonObject>>(
            Assert.Single(document.Included!, resource => resource.Type == "companies"));
        Assert.Equal(new CompanyAttributes("Acme"),
            company.Attributes!.Deserialize<CompanyAttributes>(JsonApiSerializer.Options));
    }

    [Fact]
    public void A_to_one_linkage_document_reads_identifier_or_null()
    {
        var set = Wire.Roundtrip(new ToOneLinkageDocument
        {
            Data = ResourceIdentifier.Of<CompanyAttributes>("7"),
        });
        var cleared = Wire.Roundtrip(new ToOneLinkageDocument { Data = null });

        Assert.Equal(new ResourceIdentifier("companies", "7"), set.Data);
        Assert.Null(cleared.Data);
    }

    [Fact]
    public void A_linkage_document_without_a_data_member_is_rejected()
    {
        var body = new JsonObject
        {
            ["links"] = new JsonObject { ["self"] = "/x" },
        };

        Assert.ThrowsAny<JsonException>(() =>
            JsonApiSerializer.Deserialize<ToOneLinkageDocument>(body.ToJsonString()));
    }

    [Fact]
    public void An_error_document_reads_its_error_objects()
    {
        var document = Wire.Roundtrip(new ErrorDocument
        {
            Errors =
            [
                new Error
                {
                    Status = "422",
                    Title = "Validation failed",
                    Detail = "firstName is required",
                    Source = new ErrorSource { Pointer = "/data/attributes/firstName" },
                },
            ],
        });

        var error = Assert.Single(document.Errors);
        Assert.Equal("422", error.Status);
        Assert.Equal("/data/attributes/firstName", error.Source!.Pointer);
    }

    private static ResourceDocument<ContactAttributes> Contact(
        ContactAttributes? attributes = null,
        Dictionary<string, Relationship>? relationships = null) =>
        new()
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Attributes = attributes,
                Relationships = relationships,
            },
        };

    private static ResourceDocument<ContactAttributes> Parse(JsonObject body) =>
        JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes>>(body.ToJsonString())!;
}
