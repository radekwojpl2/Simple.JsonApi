using System.Text.Json.Nodes;

namespace JsonApiLite.Tests;

/// <summary>Complexity tier 3: whole request/response cycles — a client writes a document, a
/// "server" reads it off the wire, acts on the tri-state semantics, and answers with a document
/// of its own.</summary>
public class RequestResponseScenarioTests
{
    [Fact]
    public void A_create_request_becomes_a_created_response()
    {
        // Client: build and serialize a create request — no id, that's the server's to assign.
        var request = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Attributes = new DealAttributes("Enterprise licence", 250000m, "prospecting"),
                Relationships = new DealRelationships
                {
                    Company = Relationship.ToOne<CompanyAttributes>("7"),
                    Owner = Relationship.ToOne<UserAttributes>("9"),
                },
            },
        };
        var wire = JsonApiSerializer.Serialize(request);

        // Server: read it back, check what a handler would check, assign the id.
        var received = JsonApiSerializer
            .Deserialize<ResourceDocument<DealAttributes, DealRelationships>>(wire)!;
        Assert.Null(received.Data!.Id);
        Assert.NotNull(received.Data.Relationships?.Company?.Data);
        Assert.NotNull(received.Data.Relationships?.Owner?.Data);

        var response = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = received.Data with { Id = "42", Links = new Links { Self = "/deals/42" } },
            Links = new Links { Self = "/deals/42" },
        };

        Assert.Equal(
            """{"data":{"type":"deals","id":"42","attributes":{"title":"Enterprise licence","amount":250000,"stage":"prospecting"},"relationships":{"company":{"data":{"type":"companies","id":"7"}},"owner":{"data":{"type":"users","id":"9"}}},"links":{"self":"/deals/42"}},"links":{"self":"/deals/42"}}""",
            JsonApiSerializer.Serialize(response));
    }

    [Fact]
    public void An_update_request_merges_onto_current_state()
    {
        var currentAttributes = new DealAttributes(
            "Enterprise licence", 250000m, "prospecting", new DateOnly(2026, 1, 15));
        var currentOwnerId = "9";
        var currentCompanyId = "7";
        IReadOnlyList<string> currentContactIds = ["1", "2"];

        var patch = Wire.Roundtrip(new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Attributes = new DealAttributes(Stage: "negotiation"),
                Relationships = new DealRelationships
                {
                    Company = Relationship.EmptyToOne(),
                    Contacts = Relationship.ToMany<ContactAttributes>([]),
                },
            },
        });

        // Attributes: members the patch left null keep their current values.
        var patched = patch.Data!.Attributes!;
        var mergedAttributes = currentAttributes with
        {
            Title = patched.Title ?? currentAttributes.Title,
            Amount = patched.Amount ?? currentAttributes.Amount,
            Stage = patched.Stage ?? currentAttributes.Stage,
            CloseDate = patched.CloseDate ?? currentAttributes.CloseDate,
        };

        // Relationships: null member keeps, data null clears, empty array clears the set.
        var relationships = patch.Data.Relationships!;
        var mergedOwnerId = currentOwnerId;
        if (relationships.Owner is not null)
        {
            mergedOwnerId = relationships.Owner.Data?.Id;
        }
        var mergedCompanyId = currentCompanyId;
        if (relationships.Company is not null)
        {
            mergedCompanyId = relationships.Company.Data?.Id;
        }
        var mergedContactIds = currentContactIds;
        if (relationships.Contacts is not null)
        {
            mergedContactIds = [.. relationships.Contacts.Data.Select(identifier => identifier.Id)];
        }

        Assert.Equal(
            new DealAttributes("Enterprise licence", 250000m, "negotiation", new DateOnly(2026, 1, 15)),
            mergedAttributes);
        Assert.Equal("9", mergedOwnerId);
        Assert.Null(mergedCompanyId);
        Assert.Empty(mergedContactIds);
    }

    [Fact]
    public void A_relationship_endpoint_cycle_clears_and_repoints()
    {
        var links = new Links
        {
            Self = "/deals/42/relationships/company",
            Related = "/deals/42/company",
        };

        // PATCH with null data — clear, and the response reflects the now-empty relationship.
        var clear = Wire.Roundtrip(new ToOneLinkageDocument { Data = null });
        Assert.Null(clear.Data);
        Assert.Equal(
            """{"data":null,"links":{"self":"/deals/42/relationships/company","related":"/deals/42/company"}}""",
            JsonApiSerializer.Serialize(new ToOneLinkageDocument { Data = null, Links = links }));

        // PATCH with an identifier — repoint, and the response echoes the new target.
        var repoint = Wire.Roundtrip(new ToOneLinkageDocument
        {
            Data = ResourceIdentifier.Of<CompanyAttributes>("8"),
        });
        Assert.Equal(new ResourceIdentifier("companies", "8"), repoint.Data);
        Assert.Equal(
            """{"data":{"type":"companies","id":"8"},"links":{"self":"/deals/42/relationships/company","related":"/deals/42/company"}}""",
            JsonApiSerializer.Serialize(new ToOneLinkageDocument { Data = repoint.Data, Links = links }));
    }

    [Fact]
    public void Validation_errors_map_back_to_document_pointers()
    {
        var response = new ErrorDocument
        {
            Errors =
            [
                new Error
                {
                    Status = "422",
                    Title = "Validation failed",
                    Detail = "The title attribute is required.",
                    Source = new ErrorSource { Pointer = "/data/attributes/title" },
                },
                new Error
                {
                    Status = "422",
                    Title = "Validation failed",
                    Detail = "The owner relationship is required.",
                    Source = new ErrorSource { Pointer = "/data/relationships/owner" },
                },
            ],
        };

        var wire = JsonApiSerializer.Serialize(response);
        var received = JsonApiSerializer.Deserialize<ErrorDocument>(wire)!;

        var byPointer = received.Errors.ToDictionary(error => error.Source!.Pointer!);
        Assert.Equal(2, byPointer.Count);
        Assert.Equal("The title attribute is required.",
            byPointer["/data/attributes/title"].Detail);
        Assert.Equal("The owner relationship is required.",
            byPointer["/data/relationships/owner"].Detail);
        Assert.All(received.Errors, error => Assert.Equal("422", error.Status));
    }

    [Fact]
    public void A_fully_loaded_document_is_stable_across_repeated_round_trips()
    {
        var document = new ResourceDocument<DealAttributes, DealRelationships>
        {
            Data = new Resource<DealAttributes, DealRelationships>
            {
                Type = DealAttributes.ResourceType,
                Id = "42",
                Attributes = new DealAttributes(
                    "Zoë's \"mega\" deal \\ 💼 東京", 0.1m, "prospecting", new DateOnly(2027, 3, 1)),
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
                    Owner = Relationship.EmptyToOne(),
                    Contacts = Relationship.ToMany("contacts",
                        Enumerable.Range(1, 100).Select(i => i.ToString())),
                },
                Links = new Links { Self = "/deals/42" },
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
            Links = new Links { Self = "/deals/42?include=company" },
            Meta = new Meta<PageMeta>(new PageMeta(Total: 1, PageCount: 1)),
        };

        var first = JsonApiSerializer.Serialize(document);
        var reread = JsonApiSerializer
            .Deserialize<ResourceDocument<DealAttributes, DealRelationships>>(first)!;
        var second = JsonApiSerializer.Serialize(reread);
        var rereadAgain = JsonApiSerializer
            .Deserialize<ResourceDocument<DealAttributes, DealRelationships>>(second)!;

        Assert.Equal(first, second);
        Assert.Equal(second, JsonApiSerializer.Serialize(rereadAgain));
        Assert.Equal(document.Data.Attributes, rereadAgain.Data!.Attributes);
        Assert.Equal(100, rereadAgain.Data.Relationships!.Contacts!.Data.Count);
        Assert.Null(rereadAgain.Data.Relationships.Owner!.Data);
        Assert.IsType<Resource<JsonObject>>(Assert.Single(rereadAgain.Included!));
    }
}
