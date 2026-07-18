namespace JsonApiLite.Tests;

public class SerializationTests
{
    [Fact]
    public void Writes_a_single_resource_document_in_spec_member_order()
    {
        var document = new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Attributes = new ContactAttributes("Ada", "Lovelace"),
                Relationships = new Dictionary<string, Relationship>
                {
                    ["company"] = Relationship.ToOne<CompanyAttributes>("7"),
                },
                Links = new Links { Self = "/contacts/1" },
            },
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","attributes":{"firstName":"Ada","lastName":"Lovelace"},"relationships":{"company":{"data":{"type":"companies","id":"7"}}},"links":{"self":"/contacts/1"}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_null_primary_data_explicitly()
    {
        var document = new ResourceDocument<ContactAttributes> { Data = null };

        Assert.Equal("""{"data":null}""", JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_an_empty_to_one_relationship_as_data_null()
    {
        var document = Contact("manager", Relationship.EmptyToOne());

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"manager":{"data":null}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_a_to_many_relationship_as_an_identifier_array()
    {
        var document = Contact("tags", Relationship.ToMany<TagAttributes>(["3", "9"]));

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"tags":{"data":[{"type":"tags","id":"3"},{"type":"tags","id":"9"}]}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_relationship_links_alongside_data()
    {
        var document = Contact("company", Relationship.ToOne<CompanyAttributes>("7") with
        {
            Links = new Links { Self = "/contacts/1/relationships/company", Related = "/contacts/1/company" },
        });

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1","relationships":{"company":{"links":{"self":"/contacts/1/relationships/company","related":"/contacts/1/company"},"data":{"type":"companies","id":"7"}}}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Omits_document_members_that_are_null()
    {
        var document = new ResourceCollectionDocument<ContactAttributes> { Data = [] };

        Assert.Equal("""{"data":[]}""", JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_a_collection_document_with_pagination()
    {
        var document = new ResourceCollectionDocument<ContactAttributes>
        {
            Data = [new Resource<ContactAttributes> { Type = ContactAttributes.ResourceType, Id = "1" }],
            Links = new Links { Self = "/contacts?page[number]=1", Next = "/contacts?page[number]=2" },
            Meta = new Meta(Total: 3, PageCount: 2),
        };

        Assert.Equal(
            """{"data":[{"type":"contacts","id":"1"}],"links":{"self":"/contacts?page[number]=1","next":"/contacts?page[number]=2"},"meta":{"total":3,"pageCount":2}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_included_resources_with_their_concrete_attribute_types()
    {
        var document = new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes> { Type = ContactAttributes.ResourceType, Id = "1" },
            Included =
            [
                new Resource<CompanyAttributes> { Type = CompanyAttributes.ResourceType, Id = "7", Attributes = new CompanyAttributes("Acme") },
                new Resource<TagAttributes> { Type = TagAttributes.ResourceType, Id = "3", Attributes = new TagAttributes("vip") },
            ],
        };

        Assert.Equal(
            """{"data":{"type":"contacts","id":"1"},"included":[{"type":"companies","id":"7","attributes":{"name":"Acme"}},{"type":"tags","id":"3","attributes":{"label":"vip"}}]}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_an_empty_to_one_linkage_document_with_data_null()
    {
        var document = new ToOneLinkageDocument { Data = null };

        Assert.Equal("""{"data":null}""", JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_a_to_one_linkage_document_with_its_identifier()
    {
        var document = new ToOneLinkageDocument
        {
            Data = ResourceIdentifier.Of<CompanyAttributes>("7"),
            Links = new Links { Self = "/contacts/1/relationships/company" },
        };

        Assert.Equal(
            """{"data":{"type":"companies","id":"7"},"links":{"self":"/contacts/1/relationships/company"}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_a_to_many_linkage_document_as_an_identifier_array()
    {
        var document = new ToManyLinkageDocument
        {
            Data = [ResourceIdentifier.Of<TagAttributes>("3"), ResourceIdentifier.Of<TagAttributes>("9")],
        };

        Assert.Equal(
            """{"data":[{"type":"tags","id":"3"},{"type":"tags","id":"9"}]}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Writes_an_error_document_omitting_unknown_members()
    {
        var document = new ErrorDocument
        {
            Errors =
            [
                new Error
                {
                    Status = "404",
                    Title = "Not found",
                    Source = new ErrorSource { Pointer = "/data" },
                },
            ],
        };

        Assert.Equal(
            """{"errors":[{"status":"404","title":"Not found","source":{"pointer":"/data"}}]}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void A_full_document_survives_a_round_trip_unchanged()
    {
        var document = new ResourceDocument<ContactAttributes>
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Attributes = new ContactAttributes("Ada", "Lovelace"),
                Relationships = new Dictionary<string, Relationship>
                {
                    ["company"] = Relationship.ToOne<CompanyAttributes>("7"),
                    ["manager"] = Relationship.EmptyToOne(),
                    ["tags"] = Relationship.ToMany<TagAttributes>(["3"]),
                },
                Links = new Links { Self = "/contacts/1" },
            },
            Included =
            [
                new Resource<CompanyAttributes> { Type = CompanyAttributes.ResourceType, Id = "7", Attributes = new CompanyAttributes("Acme") },
            ],
            Meta = new Meta(Total: 1, PageCount: 1),
        };

        var json = JsonApiSerializer.Serialize(document);
        var reread = JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes>>(json);

        Assert.Equal(json, JsonApiSerializer.Serialize(reread));
    }

    private static ResourceDocument<ContactAttributes> Contact(string name, Relationship relationship) =>
        new()
        {
            Data = new Resource<ContactAttributes>
            {
                Type = ContactAttributes.ResourceType,
                Id = "1",
                Relationships = new Dictionary<string, Relationship> { [name] = relationship },
            },
        };
}
