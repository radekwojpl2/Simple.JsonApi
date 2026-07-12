using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>Checks the write endpoints against the normative RFC 2119 sentences of the JSON:API
/// 1.1 "Creating, Updating and Deleting Resources" section (https://jsonapi.org/format/#crud).
/// Requests are spec-shaped — JSON:API resource documents sent as application/vnd.api+json — with
/// no accommodation for this API's plain-JSON write contract, so failures here mark exactly where
/// the API deviates from the specification.</summary>
[Collection(ApiCollection.Name)]
public class CrudSpecComplianceTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>The deviation ledger: these tests fail because the write endpoints accept flat
    /// JSON DTOs instead of JSON:API resource documents — a documented design choice, not a bug.
    /// Remove the Skip to see exactly where a strict JSON:API client would break.</summary>
    private const string FlatWriteContract =
        "Deviation by design: write endpoints accept flat JSON DTOs, not JSON:API resource documents.";

    // ── Creating Resources (§ crud-creating) ────────────────────────────────────────────────────

    /// <summary>"The request MUST include a single resource object as primary data. The resource
    /// object MUST contain at least a type member." and "If the requested resource has been
    /// created successfully ... the server MUST return a 201 Created response" whose document
    /// "contains the primary resource created". "The response SHOULD include a Location header
    /// identifying the location of the newly created resource." and "If the resource object
    /// returned by the response contains a self key in its links member and a Location header is
    /// provided, the value of the self member MUST match the value of the Location header."</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task CreatingResources_SpecShapedResourceObject_Returns201WithLocationMatchingSelf()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(CompanyAndOwner);

        // Act — a fully spec-compliant creation request.
        var response = await Client.PostAsync(Routes.Deals, JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                attributes = new { title = "Spec-shaped deal", amount = 1000 },
                relationships = new
                {
                    company = new { data = new { type = ResourceTypes.Companies, id = company.Id.ToString() } },
                    owner = new { data = new { type = ResourceTypes.Users, id = owner.Id.ToString() } }
                }
            }
        }));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(ResourceTypes.Deals, document[Doc.Data]![Doc.Type]!.GetValue<string>());
        Assert.Equal(location, document[Doc.Data]![Doc.Links]![Doc.Self]!.GetValue<string>());
    }

    /// <summary>"A server MUST return 403 Forbidden in response to an unsupported request to
    /// create a resource with a client-generated ID." — this API assigns ids itself, so a
    /// client-supplied id must be rejected with 403, not ignored.</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task CreatingResources_UnsupportedClientGeneratedId_Returns403()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(CompanyAndOwner);

        // Act
        var response = await Client.PostAsync(Routes.Deals, JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                id = "424242",
                attributes = new { title = "Client picked the id", amount = 1000 },
                relationships = new
                {
                    company = new { data = new { type = ResourceTypes.Companies, id = company.Id.ToString() } },
                    owner = new { data = new { type = ResourceTypes.Users, id = owner.Id.ToString() } }
                }
            }
        }));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request that references a
    /// related resource that does not exist."</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task CreatingResources_ReferencedRelatedResourceMissing_Returns404()
    {
        // Arrange — only the owner exists; the company relationship points into the void.
        var owner = await ArrangeAsync(db => db.Users.Add(Rows.User()).Entity);

        // Act
        var response = await Client.PostAsync(Routes.Deals, JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                attributes = new { title = "Dangling company reference", amount = 1000 },
                relationships = new
                {
                    company = new { data = new { type = ResourceTypes.Companies, id = "99999" } },
                    owner = new { data = new { type = ResourceTypes.Users, id = owner.Id.ToString() } }
                }
            }
        }));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>"A server MUST return 409 Conflict when processing a POST request in which the
    /// resource object's type is not among the type(s) that constitute the collection represented
    /// by the endpoint."</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task CreatingResources_TypeNotMatchingCollection_Returns409()
    {
        // Act — a companies resource object posted to the deals collection.
        var response = await Client.PostAsync(Routes.Deals, JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Companies,
                attributes = new { name = "Wrong collection" }
            }
        }));

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
    [Fact(Skip = FlatWriteContract)]
    public async Task UpdatingResources_SpecShapedPatch_AppliesAttributesAndKeepsMissingOnes()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Migration project", Rows.Company(), Rows.User(), amount: 5000m)).Entity);

        // Act
        var response = await Client.PatchAsync($"{Routes.Deals}/{deal.Id}", JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                id = deal.Id.ToString(),
                attributes = new { stage = "qualified" }
            }
        }));

        // Assert — a successful update returns 200 with a document or 204; the patched attribute
        // changed and the missing ones kept their current values.
        AssertSuccessfulWrite(response);
        var reloaded = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}");
        var attributes = reloaded[Doc.Data]![Doc.Attributes]!;
        Assert.Equal("qualified", attributes[Attr.Stage]!.GetValue<string>());
        Assert.Equal("Migration project", attributes[Attr.Title]!.GetValue<string>());
        Assert.Equal(5000m, attributes[Attr.Amount]!.GetValue<decimal>());
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request to modify a resource
    /// that does not exist."</summary>
    [Fact]
    public async Task UpdatingResources_UnknownResource_Returns404()
    {
        // Act
        var response = await Client.PatchAsync($"{Routes.Deals}/99999", JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                id = "99999",
                attributes = new { stage = "won" }
            }
        }));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>"A server MUST return 409 Conflict when processing a PATCH request in which the
    /// resource object's type or id do not match the server's endpoint."</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task UpdatingResources_TypeOrIdMismatch_Returns409()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Mismatched patch target", Rows.Company(), Rows.User())).Entity);

        // Act — the document claims to be a different resource than the endpoint addresses.
        var response = await Client.PatchAsync($"{Routes.Deals}/{deal.Id}", JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Companies,
                id = "77777",
                attributes = new { name = "Not a deal" }
            }
        }));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request that references a
    /// related resource that does not exist." (updating variant)</summary>
    [Fact(Skip = FlatWriteContract)]
    public async Task UpdatingResources_ReferencedRelatedResourceMissing_Returns404()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Repointing at nothing", Rows.Company(), Rows.User())).Entity);

        // Act
        var response = await Client.PatchAsync($"{Routes.Deals}/{deal.Id}", JsonApiBody(new
        {
            data = new
            {
                type = ResourceTypes.Deals,
                id = deal.Id.ToString(),
                relationships = new
                {
                    company = new { data = new { type = ResourceTypes.Companies, id = "99999" } }
                }
            }
        }));

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
        var response = await Client.PatchAsync($"{Routes.Deals}/{deal.Id}/relationships/contact",
            JsonApiBody(new
            {
                data = new { type = ResourceTypes.Contacts, id = contact.Id.ToString() }
            }));

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
        AssertSuccessfulWrite(response);
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

    /// <summary>Serializes a spec-shaped document and sends it as application/vnd.api+json with no
    /// media type parameters (a charset parameter would rightly draw a 415).</summary>
    private static HttpContent JsonApiBody(object document)
    {
        var content = new StringContent(JsonSerializer.Serialize(document), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(JsonApiMediaTypes.JsonApi);
        return content;
    }

    /// <summary>The spec's successful-write contract: 200 OK with a response document, or 204 No
    /// Content.</summary>
    private static void AssertSuccessfulWrite(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.True(response.Content.Headers.ContentLength > 0,
                "A 200 OK write response must carry a response document.");
            return;
        }
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static (Company Company, User Owner) CompanyAndOwner(AppDbContext db) =>
        (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
}
