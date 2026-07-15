using System.Net;
using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>Checks the write endpoints against the normative RFC 2119 sentences of the JSON:API
/// 1.1 "Creating, Updating and Deleting Resources" section (https://jsonapi.org/format/#crud).
/// Requests are spec-shaped — JSON:API resource documents sent as application/vnd.api+json, the
/// API's only write contract. Well-formed documents come from Document.Post/Patch/Linkage; the
/// rejection tests use the explicitly named non-conformant builders (Document.PostWithoutType,
/// PostWithArrayData, PostWithClientGeneratedId, PatchWithDatalessRelationship), which exist only
/// for probing shapes the server must refuse. Each test quotes verbatim the sentence it
/// enforces.</summary>
[Collection(ApiCollection.Name)]
public class CrudSpecComplianceTests(ApiFactory factory) : ApiTestBase(factory)
{
    // ── Creating Resources (§ crud-creating) ────────────────────────────────────────────────────

    /// <summary>"The request MUST include a single resource object as primary data. The resource
    /// object MUST contain at least a type member." and "If the requested resource has been
    /// created successfully ... the server MUST return a 201 Created response" whose document
    /// "contains the primary resource created". "The response SHOULD include a Location header
    /// identifying the location of the newly created resource." and "If the resource object
    /// returned by the response contains a self key in its links member and a Location header is
    /// provided, the value of the self member MUST match the value of the Location header."</summary>
    [Fact]
    public async Task CreatingResources_SpecShapedResourceObject_Returns201WithLocationMatchingSelf()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(CompanyAndOwner);

        // Act — a fully spec-compliant creation request.
        var response = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new { title = "Spec-shaped deal", amount = 1000 },
            (Rel.Company, ResourceTypes.Companies, company.Id),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(ResourceTypes.Deals, document[Doc.Data]![Doc.Type]!.GetValue<string>());
        Assert.Equal(location, document[Doc.Data]![Doc.Links]![Doc.Self]!.GetValue<string>());
    }

    /// <summary>"The resource object MUST contain at least a type member." — a resource object
    /// without one cannot be processed and draws a 400.</summary>
    [Fact]
    public async Task CreatingResources_MissingTypeMember_Returns400()
    {
        // Act — attributes only, no type.
        var response = await Client.PostJsonApiAsync(Routes.Deals,
            Document.PostWithoutType(new { title = "No type member" }));

        // Assert
        await response.ReadProblemAsync(400);
    }

    /// <summary>"The request MUST include a single resource object as primary data." — an array of
    /// resource objects is not a single resource object and draws a 400.</summary>
    [Fact]
    public async Task CreatingResources_ArrayPrimaryData_Returns400()
    {
        // Act
        var response = await Client.PostJsonApiAsync(Routes.Deals,
            Document.PostWithArrayData(ResourceTypes.Deals, new { title = "In an array" }));

        // Assert
        await response.ReadProblemAsync(400);
    }

    /// <summary>"A server MUST return 403 Forbidden in response to an unsupported request to
    /// create a resource with a client-generated ID." — this API assigns ids itself, so a
    /// client-supplied id must be rejected with 403, not ignored.</summary>
    [Fact]
    public async Task CreatingResources_UnsupportedClientGeneratedId_Returns403()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(CompanyAndOwner);

        // Act
        var response = await Client.PostJsonApiAsync(Routes.Deals,
            Document.PostWithClientGeneratedId(ResourceTypes.Deals, "424242",
                new { title = "Client picked the id", amount = 1000 },
                (Rel.Company, ResourceTypes.Companies, company.Id),
                (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request that references a
    /// related resource that does not exist."</summary>
    [Fact]
    public async Task CreatingResources_ReferencedRelatedResourceMissing_Returns404()
    {
        // Arrange — only the owner exists; the company relationship points into the void.
        var owner = await ArrangeAsync(db => db.Users.Add(Rows.User()).Entity);

        // Act
        var response = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new { title = "Dangling company reference", amount = 1000 },
            (Rel.Company, ResourceTypes.Companies, 99999),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>"A server MUST return 409 Conflict when processing a POST request in which the
    /// resource object's type is not among the type(s) that constitute the collection represented
    /// by the endpoint."</summary>
    [Fact]
    public async Task CreatingResources_TypeNotMatchingCollection_Returns409()
    {
        // Act — a companies resource object posted to the deals collection.
        var response = await Client.PostJsonApiAsync(Routes.Deals,
            Document.Post(ResourceTypes.Companies, new { name = "Wrong collection" }));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Updating Resources (§ crud-updating) ────────────────────────────────────────────────────

    /// <summary>"The PATCH request MUST include a single resource object as primary data. The
    /// resource object MUST contain type and id members." plus "If a request does not include all
    /// of the attributes for a resource, the server MUST interpret the missing attributes as if
    /// they were included with their current values. The server MUST NOT interpret missing
    /// attributes as null values." — a spec-shaped patch of one attribute must apply it and leave
    /// every other attribute untouched.</summary>
    [Fact]
    public async Task UpdatingResources_SpecShapedPatch_AppliesAttributesAndKeepsMissingOnes()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Migration project", Rows.Company(), Rows.User(), amount: 5000m)).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Deals, deal.Id, new { stage = "qualified" }));

        // Assert — a successful update returns 200 with a document or 204; the patched attribute
        // changed and the missing ones kept their current values.
        response.ShouldBeSuccessfulWrite();
        var reloaded = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}");
        var attributes = reloaded[Doc.Data]![Doc.Attributes]!;
        Assert.Equal("qualified", attributes[Attr.Stage]!.GetValue<string>());
        Assert.Equal("Migration project", attributes[Attr.Title]!.GetValue<string>());
        Assert.Equal(5000m, attributes[Attr.Amount]!.GetValue<decimal>());
    }

    /// <summary>"If a request does not include all of the relationships for a resource, the server
    /// MUST interpret the missing relationships as if they were included with their current
    /// values. It MUST NOT interpret them as null or empty values." — an attributes-only patch
    /// leaves every relationship linkage intact.</summary>
    [Fact]
    public async Task UpdatingResources_MissingRelationships_KeepCurrentValues()
    {
        // Arrange — a deal with all three relationships populated.
        var deal = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return db.Deals.Add(Rows.Deal("Fully linked", company, Rows.User(),
                contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
        });

        // Act — no relationships member at all.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Deals, deal.Id, new { stage = "proposal" }));

        // Assert — every linkage still points where it did before the patch.
        response.ShouldBeSuccessfulWrite();
        var reloaded = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}");
        var relationships = reloaded[Doc.Data]![Doc.Relationships]!;
        Assert.Equal(deal.CompanyId.ToString(), relationships[Rel.Company]![Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal(deal.ContactId!.Value.ToString(), relationships[Rel.Contact]![Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal(deal.OwnerId.ToString(), relationships[Rel.Owner]![Doc.Data]![Doc.Id]!.GetValue<string>());
    }

    /// <summary>"If a relationship is provided in the relationships member of a resource object in
    /// a PATCH request, its value MUST be a relationship object with a data member." — a
    /// relationship entry without one is malformed and draws a 400.</summary>
    [Fact]
    public async Task UpdatingResources_RelationshipWithoutDataMember_Returns400()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Malformed relationship", Rows.Company(), Rows.User())).Entity);

        // Act — the contact relationship carries no data member.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.PatchWithDatalessRelationship(ResourceTypes.Deals, deal.Id, Rel.Contact));

        // Assert
        await response.ReadProblemAsync(400);
    }

    /// <summary>"If a relationship is provided in the relationships member of a resource object in
    /// a PATCH request, its value MUST be a relationship object with a data member. The
    /// relationship's value will be replaced with the value specified in this member." — null
    /// resource linkage empties the to-one relationship. Deal-specific: deal→contact is this API's
    /// nullable to-one.</summary>
    [Fact]
    public async Task UpdatingResources_NullToOneLinkage_ClearsTheRelationship()
    {
        // Arrange — a deal that has a contact.
        var deal = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return db.Deals.Add(Rows.Deal("Losing its contact", company, Rows.User(),
                contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
        });

        // Act — a null target id emits the clearing "data": null linkage.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Deals, deal.Id, attributes: null,
                (Rel.Contact, ResourceTypes.Contacts, null)));

        // Assert — the relationship endpoint now serves an explicit data: null.
        response.ShouldBeSuccessfulWrite();
        var linkage = JsonNode.Parse(await
            (await Client.GetAsync($"{Routes.Deals}/{deal.Id}/relationships/contact"))
            .Content.ReadAsStringAsync())!.AsObject();
        Assert.True(linkage.TryGetPropertyValue(Doc.Data, out var data));
        Assert.Null(data);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request to modify a resource
    /// that does not exist."</summary>
    [Fact]
    public async Task UpdatingResources_UnknownResource_Returns404()
    {
        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/99999",
            Document.Patch(ResourceTypes.Deals, 99999, new { stage = "won" }));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>"A server MUST return 409 Conflict when processing a PATCH request in which the
    /// resource object's type or id do not match the server's endpoint."</summary>
    [Fact]
    public async Task UpdatingResources_TypeOrIdMismatch_Returns409()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Mismatched patch target", Rows.Company(), Rows.User())).Entity);

        // Act — the document claims to be a different resource than the endpoint addresses.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Companies, 77777, new { name = "Not a deal" }));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request that references a
    /// related resource that does not exist." (updating variant)</summary>
    [Fact]
    public async Task UpdatingResources_ReferencedRelatedResourceMissing_Returns404()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Repointing at nothing", Rows.Company(), Rows.User())).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Deals, deal.Id, attributes: null,
                (Rel.Company, ResourceTypes.Companies, 99999)));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Updating Relationships (§ crud-updating-relationships) ──────────────────────────────────

    /// <summary>"A to-one relationship can be updated by sending a PATCH request to a URL from a
    /// to-one relationship link. The PATCH request MUST include a top-level member named data
    /// containing one of: a resource identifier object [or] null. If the relationship is updated
    /// successfully then the server MUST return a successful response." and, for servers that do
    /// not support it: "A server MUST return 403 Forbidden in response to an unsupported request
    /// to update a relationship." — anything other than success or 403 is non-conformant, because
    /// the API advertises this URL as a relationship self link.</summary>
    [Fact]
    public async Task UpdatingRelationships_ToOnePatch_SucceedsOrReturns403()
    {
        // Arrange — a deal without a contact and an existing contact to point it at.
        var (deal, contact) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return (db.Deals.Add(Rows.Deal("Needs a contact", company, Rows.User())).Entity,
                db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity);
        });

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}/relationships/contact",
            Document.Linkage((ResourceTypes.Contacts, contact.Id)));

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.Forbidden,
            $"PATCH on an advertised relationship link must succeed (200/204) or be refused with " +
            $"403 Forbidden, but returned {(int)response.StatusCode}.");
    }

    // ── Deleting Resources (§ crud-deleting) ────────────────────────────────────────────────────

    /// <summary>"If a deletion request is successful, the server MUST return either a 200 OK
    /// status code and response document or a 204 No Content status code."</summary>
    [Fact]
    public async Task DeletingResources_Success_Returns200WithDocumentOr204()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Short-lived", Rows.Company(), Rows.User())).Entity);

        // Act
        var response = await Client.DeleteAsync($"{Routes.Deals}/{deal.Id}");

        // Assert
        response.ShouldBeSuccessfulWrite();
    }

    /// <summary>"A server SHOULD return a 404 Not Found status code if a deletion request fails
    /// due to the resource not existing."</summary>
    [Fact]
    public async Task DeletingResources_UnknownResource_Returns404()
    {
        // Act
        var response = await Client.DeleteAsync($"{Routes.Deals}/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static (Company Company, User Owner) CompanyAndOwner(AppDbContext db) =>
        (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
}
