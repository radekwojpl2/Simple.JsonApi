using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests;

/// <summary>Multi-step scenarios that chain several endpoints the way a real client would, instead
/// of probing one endpoint per test. Steps are labelled instead of the usual single AAA triple,
/// because each step's assertion is the arrangement for the next. Each scenario arranges its own
/// starting graph; every response body is matched in full against the Expect golden model.</summary>
[Collection(ApiCollection.Name)]
public class WorkflowScenarioTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task DealPipeline_LeadToWon_TracksEveryTransition()
    {
        // Arrange — the account and rep the new business lands on, and the field the pipeline tracks.
        var (company, owner) = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number"));
            return (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
        });

        // Step 1 — a new prospect calls in: contact first, then a deal attached to them.
        var beata = await PostContactAsync("Beata", "Sowa", "beata.sowa@acme.example.com", company.Id);

        var dealResponse = await Client.PostAsJsonAsync(Routes.Deals, new
        {
            title = "Conveyor retrofit",
            amount = 20000,
            companyId = company.Id,
            contactId = beata.Id,
            ownerId = owner.Id,
            customFields = new { probability = 10 }
        });
        Assert.Equal(HttpStatusCode.Created, dealResponse.StatusCode);
        var dealLocation = dealResponse.Headers.Location!.ToString();
        var dealBody = JsonNode.Parse(await dealResponse.Content.ReadAsStringAsync())!;

        var deal = new Deal
        {
            Id = int.Parse(dealBody[Doc.Data]![Doc.Id]!.GetValue<string>()),
            Title = "Conveyor retrofit", Amount = 20000m, Stage = DealStages.Lead,
            CompanyId = company.Id, ContactId = beata.Id, OwnerId = owner.Id
        };
        dealBody.ShouldMatchExactly(Document.Single(Document.Deal(deal, new { probability = 10 })));

        // Step 2 — work the deal through the pipeline; each transition bumps the win probability.
        foreach (var (stage, probability) in new[] { ("qualified", 30), ("proposal", 60), ("won", 100) })
        {
            var patched = await Client.PatchAsJsonAsync(dealLocation,
                new { stage, customFields = new { probability } });
            Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);

            deal.Stage = stage;
            var current = await Client.GetDocumentAsync(dealLocation);
            current.ShouldMatchExactly(Document.Single(Document.Deal(deal, new { probability })));
        }

        // Step 3 — winning the deal makes it visible to the stage filter.
        var wonDeals = await Client.GetDocumentAsync($"{Routes.Deals}?filter[stage]=won");
        wonDeals.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                "filter[stage]=won",
                1, 25, total: 1,
                [Document.Deal(deal, new { probability = 100 })]));

        // Step 4 — the compound document ties the whole story together: deal + contact + owner.
        var query = $"include={Rel.Contact},{Rel.Owner}";
        var full = await Client.GetDocumentAsync($"{dealLocation}?{query}");
        full.ShouldMatchExactly(Document.Single(Document.Deal(deal, new { probability = 100 }),
            Document.Contact(beata), Document.User(owner)));

        // Step 5 — account handoff: the deal moves to a different contact; the linkage must follow.
        var igor = await PostContactAsync("Igor", "Baran", "igor.baran@acme.example.com", company.Id);
        await Client.PatchAsJsonAsync(dealLocation, new { contactId = igor.Id });

        var reassigned = await Client.GetDocumentAsync($"{dealLocation}/relationships/contact");
        reassigned.ShouldMatchExactly(Document.Linkage(
            $"{dealLocation}/relationships/contact", $"{dealLocation}/contact",
            (ResourceTypes.Contacts, igor.Id)));

        // Step 6 — tear down: deleting the deal and contacts leaves both collections empty.
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(dealLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"{Routes.Contacts}/{beata.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"{Routes.Contacts}/{igor.Id}")).StatusCode);

        (await Client.GetDocumentAsync(Routes.Deals))
            .ShouldMatchExactly(
                Document.Page(
                    Routes.Deals,
                    null,
                    1, 25, total: 0,
                    []));
        (await Client.GetDocumentAsync(Routes.Contacts))
            .ShouldMatchExactly(
                Document.Page(
                    Routes.Contacts,
                    null,
                    1, 25, total: 0,
                    []));
    }

    [Fact]
    public async Task DealLoss_MarkingDealLost_RecordsCompetitorAndJoinsLossReport()
    {
        // Arrange — a live deal in the pipeline, and an already-lost one the report shows alongside it.
        var (company, owner, warehouse) = await ArrangeAsync(db =>
        {
            var globex = Rows.Company("Globex Logistics");
            var marcus = Rows.User("Marcus Webb");
            var competitor = Rows.Field(ResourceTypes.Deals, Attr.Competitor);
            var probability = Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number");
            var alreadyLost = Rows.Deal("Warehouse expansion", globex, marcus, stage: "lost");
            db.AddRange(competitor, probability, alreadyLost);
            db.SaveChanges();

            db.CustomFieldValues.Add(Rows.Value(competitor, alreadyLost.Id, "FleetCo"));
            return (globex, marcus, alreadyLost);
        });

        var created = await Client.PostAsJsonAsync(Routes.Deals, new
        {
            title = "Cold chain monitoring",
            amount = 18000,
            companyId = company.Id,
            ownerId = owner.Id,
            customFields = new { probability = 55 }
        });
        var location = created.Headers.Location!.ToString();
        var coldChain = new Deal
        {
            Id = int.Parse(JsonNode.Parse(await created.Content.ReadAsStringAsync())!
                [Doc.Data]![Doc.Id]!.GetValue<string>()),
            Title = "Cold chain monitoring", Amount = 18000m, Stage = "lost",
            CompanyId = company.Id, OwnerId = owner.Id
        };

        // Step 1 — the deal falls through: mark it lost and record who won it instead.
        var patched = await Client.PatchAsJsonAsync(location, new
        {
            stage = "lost",
            customFields = new { probability = 0, competitor = "ColdTrack" }
        });
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);

        var lost = await Client.GetDocumentAsync(location);
        lost.ShouldMatchExactly(Document.Single(
            Document.Deal(coldChain, new { probability = 0, competitor = "ColdTrack" })));

        // Step 2 — the loss report shows it alongside the arranged lost deal, each with its competitor.
        var lostDeals = await Client.GetDocumentAsync($"{Routes.Deals}?filter[stage]=lost");
        lostDeals.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                "filter[stage]=lost",
                1, 25, total: 2,
                [
                    Document.Deal(warehouse, new { competitor = "FleetCo" }),
                    Document.Deal(coldChain, new { probability = 0, competitor = "ColdTrack" })
                ]));

        // Step 3 — losing a deal must not leak it into any active-pipeline view: with both deals
        // lost, every other stage filter returns an empty page.
        foreach (var stage in new[] { DealStages.Lead, "qualified", "proposal", "won" })
        {
            var active = await Client.GetDocumentAsync($"{Routes.Deals}?filter[stage]={stage}");
            active.ShouldMatchExactly(
                Document.Page(
                    Routes.Deals,
                    $"filter[stage]={stage}",
                    1, 25, total: 0,
                    []));
        }

        // Step 4 — deleting the deal takes it back out of the loss report.
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(location)).StatusCode);
        var remaining = await Client.GetDocumentAsync($"{Routes.Deals}?filter[stage]=lost");
        remaining.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                "filter[stage]=lost",
                1, 25, total: 1,
                [Document.Deal(warehouse, new { competitor = "FleetCo" })]));
    }

    [Fact]
    public async Task CustomFields_SequentialPatches_MergeIntoExistingSet()
    {
        // Arrange — the two contact fields the patches will touch.
        var company = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.AddRange(
                Rows.Field(ResourceTypes.Contacts, Attr.LeadSource),
                Rows.Field(ResourceTypes.Contacts, Attr.NewsletterOptIn, dataType: "boolean"));
            return db.Companies.Add(Rows.Company()).Entity;
        });

        var created = await Client.PostAsJsonAsync(Routes.Contacts, new
        {
            firstName = "Rafal",
            lastName = "Gajda",
            companyId = company.Id,
            customFields = new { leadSource = "webinar" }
        });
        var location = created.Headers.Location!.ToString();
        var rafal = new Contact
        {
            Id = int.Parse(JsonNode.Parse(await created.Content.ReadAsStringAsync())!
                [Doc.Data]![Doc.Id]!.GetValue<string>()),
            FirstName = "Rafal", LastName = "Gajda", Email = "", Phone = "", CompanyId = company.Id
        };

        // Act + Assert — patching a different custom field must add it without touching the existing one...
        await Client.PatchAsJsonAsync(location, new { customFields = new { newsletterOptIn = true } });
        var afterAdd = await Client.GetDocumentAsync(location);
        afterAdd.ShouldMatchExactly(Document.Single(
            Document.Contact(rafal, new { leadSource = "webinar", newsletterOptIn = true })));

        // Act + Assert — ...and patching the original field must update it while keeping the added one.
        await Client.PatchAsJsonAsync(location, new { customFields = new { leadSource = "referral" } });
        var afterUpdate = await Client.GetDocumentAsync(location);
        afterUpdate.ShouldMatchExactly(Document.Single(
            Document.Contact(rafal, new { leadSource = "referral", newsletterOptIn = true })));
    }

    [Fact]
    public async Task Hypermedia_RelationshipLinks_AreNavigable()
    {
        // Arrange — a deal with all three relationships set.
        var deal = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return db.Deals.Add(Rows.Deal("ERP integration", company, Rows.User(),
                contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
        });
        var document = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}");
        document.ShouldMatchExactly(Document.Single(Document.Deal(deal)));

        // Act + Assert — every relationship the resource advertises must be navigable via its own
        // links, and the linkage and related documents must be exactly the expected shapes.
        var relationships = new (string Name, string Type, int TargetId, ResourceExpectation Related)[]
        {
            (Rel.Company, ResourceTypes.Companies, deal.CompanyId, Document.Company(deal.Company!)),
            (Rel.Contact, ResourceTypes.Contacts, deal.ContactId!.Value, Document.Contact(deal.Contact!)),
            (Rel.Owner, ResourceTypes.Users, deal.OwnerId, Document.User(deal.Owner!))
        };
        foreach (var (name, type, targetId, related) in relationships)
        {
            var relationship = document[Doc.Data]![Doc.Relationships]![name]!;
            var selfUrl = relationship[Doc.Links]![Doc.Self]!.GetValue<string>();
            var relatedUrl = relationship[Doc.Links]![Doc.Related]!.GetValue<string>();

            (await Client.GetDocumentAsync(selfUrl))
                .ShouldMatchExactly(Document.Linkage(selfUrl, relatedUrl, (type, targetId)));
            (await Client.GetDocumentAsync(relatedUrl))
                .ShouldMatchExactly(Document.Related(relatedUrl, related));
        }
    }

    [Fact]
    public async Task Pagination_FollowingNextLinks_VisitsEveryDealExactlyOnce()
    {
        // Arrange — enough deals to spread the collection over several pages.
        const int total = 7;
        await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var owner = Rows.User();
            for (var i = 1; i <= total; i++)
            {
                db.Deals.Add(Rows.Deal($"Deal {i:d2}", company, owner));
            }
        });

        // Act — walk the collection purely through the links the API hands back
        // (sorted, so page boundaries are deterministic).
        var visited = new List<string>();
        var pages = 0;
        var url = $"{Routes.Deals}?sort={Attr.Title}&page[size]=2";
        while (url is not null)
        {
            Assert.True(++pages <= 10, "Pagination did not terminate; is the next link ever null?");
            var page = await Client.GetDocumentAsync(url);
            visited.AddRange(page[Doc.Data]!.AsArray()
                .Select(resource => resource![Doc.Id]!.GetValue<string>()));
            url = page[Doc.Links]![Doc.Next]?.GetValue<string>()!;
        }

        // Assert — every deal exactly once, and the union of pages is the unpaged collection.
        Assert.Equal((total + 1) / 2, pages);
        Assert.Equal(total, visited.Count);
        Assert.Equal(visited.Count, visited.Distinct().Count()); // no resource twice

        var unpaged = await Client.GetDocumentAsync(Routes.Deals);
        var allIds = unpaged[Doc.Data]!.AsArray()
            .Select(resource => resource![Doc.Id]!.GetValue<string>())
            .ToHashSet();
        Assert.Equal(allIds, visited.ToHashSet());
    }

    /// <summary>POSTs a contact and returns an entity mirroring what the server stored, for
    /// building expected documents. Unsent email/phone are stored as empty strings.</summary>
    private async Task<Contact> PostContactAsync(string firstName, string lastName, string email, int companyId)
    {
        var response = await Client.PostAsJsonAsync(Routes.Contacts, new
        {
            firstName, lastName, email, companyId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new Contact
        {
            Id = int.Parse(body[Doc.Data]![Doc.Id]!.GetValue<string>()),
            FirstName = firstName, LastName = lastName, Email = email, Phone = "", CompanyId = companyId
        };
    }
}
