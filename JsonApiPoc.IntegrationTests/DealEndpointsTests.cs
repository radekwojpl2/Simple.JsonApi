using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DealEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Deal _biggest = null!;
    private Deal _smallest = null!;
    private Deal _middle = null!;

    [Fact]
    public async Task List_Default_ReturnsArrangedCollection()
    {
        // Arrange
        await ArrangeAsync(DealsWithTellingAmounts);

        // Act
        var document = await Client.GetDocumentAsync(Routes.Deals);

        // Assert — all three deals in id order, each with full attributes, company/owner
        // relationships, and links; plus pagination links and meta.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                null,
                1, 25, total: 3,
                [Document.Deal(_biggest), Document.Deal(_smallest), Document.Deal(_middle)]));
    }

    [Fact]
    public async Task List_FilterByStage_NarrowsToMatchingDeals()
    {
        // Arrange — one won deal among others, so passing means the filter excluded the rest.
        var won = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var owner = Rows.User();
            var wonDeal = db.Deals.Add(Rows.Deal("The won one", company, owner, stage: "won")).Entity;
            db.Deals.AddRange(
                Rows.Deal("Still open", company, owner, stage: "proposal"),
                Rows.Deal("The lost one", company, owner, stage: "lost"));
            return wonDeal;
        });

        // Act
        var query = "filter[stage]=won";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — exactly the won deal; meta counts the filtered set and the filter rides along
        // in the pagination links.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 1,
                [Document.Deal(won)]));
    }

    [Fact]
    public async Task List_FilterByStageLost_ReturnsLossAnalysisFields()
    {
        // Arrange — the lost deal carries a competitor and a decimal probability.
        var lost = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var owner = Rows.User();
            var lostDeal = Rows.Deal("Warehouse expansion", company, owner, stage: "lost");
            var competitor = Rows.Field(ResourceTypes.Deals, Attr.Competitor);
            var probability = Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number");
            db.AddRange(lostDeal, Rows.Deal("Support renewal", company, owner, stage: "won"),
                competitor, probability);
            db.SaveChanges(); // assigns the ids the value store references

            db.CustomFieldValues.AddRange(
                Rows.Value(competitor, lostDeal.Id, "FleetCo"),
                Rows.Value(probability, lostDeal.Id, "12.5"));
            return lostDeal;
        });

        // Act
        var query = "filter[stage]=lost";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — the competitor and the decimal probability come back typed correctly.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 1,
                [Document.Deal(lost, customFields: new { competitor = "FleetCo", probability = 12.5m })]));
    }

    [Fact]
    public async Task List_UnknownStageFilter_Returns400()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Deals}?filter[stage]=bogus", 400);

        // Assert — the detail lists the valid stages.
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid filter",
            "Unknown stage 'bogus'. Valid values: lead, qualified, proposal, won, lost."));
    }

    [Fact]
    public async Task List_SortByAmountDescending_OrdersNumericallyNotLexically()
    {
        // Arrange
        await ArrangeAsync(DealsWithTellingAmounts);

        // Act
        var query = $"sort=-{Attr.Amount}";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — a lexical sort would lead with "8000".
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 3,
                [Document.Deal(_biggest), Document.Deal(_middle), Document.Deal(_smallest)]));
    }

    [Fact]
    public async Task List_SortByAmountAscending_OrdersNumericallyNotLexically()
    {
        // Arrange
        await ArrangeAsync(DealsWithTellingAmounts);

        // Act
        var query = $"sort={Attr.Amount}";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — a lexical sort would lead with "12500".
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 3,
                [Document.Deal(_smallest), Document.Deal(_middle), Document.Deal(_biggest)]));
    }

    [Fact]
    public async Task List_SortByStageThenAmountDescending_GroupsByStageAndOrdersWithinEach()
    {
        // Arrange — amounts interleave across the two stages, so a global amount sort would mix
        // the groups; only stage-first grouping with a descending amount tie-break yields this order.
        var (leadBig, leadSmall, proposalBig, proposalSmall) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var owner = Rows.User();
            return (
                db.Deals.Add(Rows.Deal("Lead big", company, owner, stage: "lead", amount: 15000m)).Entity,
                db.Deals.Add(Rows.Deal("Lead small", company, owner, stage: "lead", amount: 5000m)).Entity,
                db.Deals.Add(Rows.Deal("Proposal big", company, owner, stage: "proposal", amount: 20000m)).Entity,
                db.Deals.Add(Rows.Deal("Proposal small", company, owner, stage: "proposal", amount: 10000m)).Entity);
        });

        // Act
        var query = $"sort={Attr.Stage},-{Attr.Amount}";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — lead group first (stage ascending), biggest amount first within each group.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 4,
                [Document.Deal(leadBig), Document.Deal(leadSmall), Document.Deal(proposalBig), Document.Deal(proposalSmall)]));
    }

    [Fact]
    public async Task List_SortWithPaging_KeepsSortInThePageLinks()
    {
        // Arrange — three deals at page size 2 leave a second page to link to.
        await ArrangeAsync(DealsWithTellingAmounts);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?sort=-{Attr.Amount}&page[size]=2");

        // Assert — the sort applies to the page, and every pagination link hands it back.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                $"sort=-{Attr.Amount}",
                number: 1, size: 2, total: 3,
                [Document.Deal(_biggest), Document.Deal(_middle)]));
    }

    [Fact]
    public async Task List_IncludeAllRelationships_SideloadsEveryType()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return db.Deals.Add(Rows.Deal("ERP integration", company, Rows.User(),
                contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
        });

        // Act
        var query = $"include={Rel.Company},{Rel.Contact},{Rel.Owner}";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — the full compound document; included carries company, contact, owner in
        // endpoint order, each as a complete resource.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 1,
                [Document.Deal(deal)],
                Document.Company(deal.Company!),
                Document.Contact(deal.Contact!),
                Document.User(deal.Owner!)));
    }

    [Fact]
    public async Task List_MiddlePage_LinksToBothNeighbours()
    {
        // Arrange
        var deals = await ArrangeAsync(db => AddDeals(db, 5));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?page[size]=2&page[number]=2");

        // Assert — 5 deals at size 2: page 2 holds deals 3 and 4; prev/next both present.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                null,
                number: 2, size: 2, total: 5,
                [Document.Deal(deals[2]), Document.Deal(deals[3])]));
    }

    [Fact]
    public async Task List_LastPartialPage_HasNoNextLink()
    {
        // Arrange
        var deals = await ArrangeAsync(db => AddDeals(db, 5));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?page[size]=2&page[number]=3");

        // Assert — exactly the last deal; the expected links carry prev but no next.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                null,
                number: 3, size: 2, total: 5,
                [Document.Deal(deals[4])]));
    }

    [Fact]
    public async Task List_PageBeyondLast_ReturnsEmptyPageWithLinks()
    {
        // Arrange
        await ArrangeAsync(db => AddDeals(db, 5));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?page[number]=99");

        // Assert — an empty data array with real totals; prev clamps to the actual last page.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                null,
                number: 99, size: 25, total: 5,
                []));
    }

    [Fact]
    public async Task List_SparseFieldset_OmitsAttributesAndRelationships()
    {
        // Arrange
        var deals = await ArrangeAsync(db => AddDeals(db, 1));

        // Act
        var query = $"fields[{ResourceTypes.Deals}]={Attr.Title}";
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?{query}");

        // Assert — only the title attribute survives; the relationships (spec "fields" too) are
        // gone entirely, while links.self stays.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Deals,
                query,
                1, 25, total: 1,
                [Document.Deal(deals[0]).Fields(Attr.Title)]));
    }

    [Fact]
    public async Task GetRelationship_DealWithoutContact_ReturnsNullData()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var linkage = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/relationships/contact");

        // Assert — the full linkage document: explicit data:null plus both links.
        linkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Deals}/{deal.Id}/relationships/contact",
            $"{Routes.Deals}/{deal.Id}/contact", identifier: null));
    }

    [Fact]
    public async Task GetRelated_DealWithoutContact_ReturnsNullData()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var related = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/contact");

        // Assert
        related.ShouldMatchExactly(Document.Related($"{Routes.Deals}/{deal.Id}/contact", resource: null));
    }

    [Fact]
    public async Task GetRelationship_Owner_AgreesWithRelatedResource()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("ERP integration", Rows.Company(), Rows.User("Sarah Chen"))).Entity);

        // Act
        var linkage = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/relationships/owner");
        var related = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/owner");

        // Assert — both documents name the arranged owner; the related one carries the full user.
        linkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Deals}/{deal.Id}/relationships/owner",
            $"{Routes.Deals}/{deal.Id}/owner",
            (ResourceTypes.Users, deal.OwnerId)));
        related.ShouldMatchExactly(Document.Related(
            $"{Routes.Deals}/{deal.Id}/owner", Document.User(deal.Owner!)));
    }

    [Fact]
    public async Task GetById_DealWithDateCustomField_ReturnsIsoDateString()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
        {
            var renewal = Rows.Deal("Support renewal", Rows.Company(), Rows.User(), stage: "won");
            var signed = Rows.Field(ResourceTypes.Deals, Attr.ContractSignedDate, dataType: "date");
            db.AddRange(renewal, signed);
            db.SaveChanges();

            db.CustomFieldValues.Add(Rows.Value(signed, renewal.Id, "2026-06-28"));
            return renewal;
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}");

        // Assert — the whole resource, with the date custom field as the stored ISO string.
        document.ShouldMatchExactly(Document.Single(
            Document.Deal(deal, customFields: new { contractSignedDate = "2026-06-28" })));
    }

    [Fact]
    public async Task Post_ValidDeal_Returns201WithTypedCustomFields()
    {
        // Arrange — the referenced company/owner and the two fields the write sets.
        var (company, owner) = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.AddRange(
                Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number"),
                Rows.Field(ResourceTypes.Deals, Attr.ContractSignedDate, dataType: "date"));
            return (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
        });

        // Act
        var created = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new
            {
                title = "Warehouse automation",
                amount = 75000,
                customFields = new { probability = 40, contractSignedDate = "2026-10-01" }
            },
            (Rel.Company, ResourceTypes.Companies, company.Id),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert — 201 with Location, and the body is the complete created resource: stage
        // defaults to lead, no closeDate, no contact relationship, typed custom fields.
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        var body = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = int.Parse(body[Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal($"{Routes.Deals}/{id}", location);

        var expected = new Deal
        {
            Id = id, Title = "Warehouse automation", Amount = 75000m, Stage = DealStages.Lead,
            CompanyId = company.Id, OwnerId = owner.Id
        };
        var expectedFields = new { probability = 40, contractSignedDate = "2026-10-01" };
        body.ShouldMatchExactly(Document.Single(Document.Deal(expected, expectedFields)));

        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(Document.Deal(expected, expectedFields)));

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Post_JsonApiResourceDocument_CreatesDeal()
    {
        // Arrange — the related rows and the custom field the document sets.
        var (company, contact, owner) = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number"));
            var company = Rows.Company();
            return (db.Companies.Add(company).Entity,
                db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity,
                db.Users.Add(Rows.User()).Entity);
        });

        // Act — a create with every attribute and relationship the document can carry.
        var created = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new
            {
                title = "Warehouse automation",
                amount = 75000,
                stage = "qualified",
                customFields = new { probability = 40 }
            },
            (Rel.Company, ResourceTypes.Companies, company.Id),
            (Rel.Contact, ResourceTypes.Contacts, contact.Id),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert — 201 with Location and the full created resource.
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        var body = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = int.Parse(body[Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal($"{Routes.Deals}/{id}", location);

        var expected = new Deal
        {
            Id = id, Title = "Warehouse automation", Amount = 75000m, Stage = "qualified",
            CompanyId = company.Id, ContactId = contact.Id, OwnerId = owner.Id
        };
        body.ShouldMatchExactly(Document.Single(Document.Deal(expected, new { probability = 40 })));
    }

    [Fact]
    public async Task Post_MissingTitle_Returns422()
    {
        // Act — relationships are present, but the title attribute is not.
        var response = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            attributes: null,
            (Rel.Company, ResourceTypes.Companies, 1),
            (Rel.Owner, ResourceTypes.Users, 1)));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed", "The 'title' field is required."));
    }

    /// <summary>The API's only write contract is JSON:API; a flat application/json body is
    /// refused up front by the content negotiation middleware.</summary>
    [Fact]
    public async Task Post_FlatJsonContentType_Returns415()
    {
        // Act
        var response = await Client.PostAsJsonAsync(Routes.Deals, new { title = "Flat JSON" });

        // Assert
        var problem = await response.ReadProblemAsync(415);
        problem.ShouldMatchExactly(Document.Problem(415, "Unsupported media type",
            "This API accepts only JSON:API request bodies; send the payload as 'application/vnd.api+json'."));
    }

    [Fact]
    public async Task Post_WrongCustomFieldType_Returns422()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number"));
            return (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
        });

        // Act
        var response = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new
            {
                title = "Mistyped probability",
                customFields = new { probability = "high" } // declared as a number field
            },
            (Rel.Company, ResourceTypes.Companies, company.Id),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "Custom field 'probability' expects a number value."));
    }

    [Fact]
    public async Task Post_NonDateValueForDateCustomField_Returns422()
    {
        // Arrange
        var (company, owner) = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Deals, Attr.ContractSignedDate, dataType: "date"));
            return (db.Companies.Add(Rows.Company()).Entity, db.Users.Add(Rows.User()).Entity);
        });

        // Act
        var response = await Client.PostJsonApiAsync(Routes.Deals, Document.Post(ResourceTypes.Deals,
            new
            {
                title = "Mistyped signature date",
                customFields = new { contractSignedDate = "not-a-date" }
            },
            (Rel.Company, ResourceTypes.Companies, company.Id),
            (Rel.Owner, ResourceTypes.Users, owner.Id)));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "Custom field 'contractSignedDate' expects a date value."));
    }

    [Fact]
    public async Task Patch_StageAndAmount_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Migration project", Rows.Company(), Rows.User())).Entity);
        var location = $"{Routes.Deals}/{deal.Id}";

        // Act
        var patched = await Client.PatchJsonApiAsync(location,
            Document.Patch(ResourceTypes.Deals, deal.Id, new { stage = "qualified", amount = 15000 }));

        // Assert — the reloaded document is the full resource with only the patched fields changed.
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
        deal.Stage = "qualified";
        deal.Amount = 15000m;
        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(Document.Deal(deal)));
    }

    [Fact]
    public async Task Patch_JsonApiResourceDocument_UpdatesAttributesAndRelationships()
    {
        // Arrange — a deal without a contact, plus the contact the patch links.
        var (deal, contact) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return (db.Deals.Add(Rows.Deal("Migration project", company, Rows.User())).Entity,
                db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity);
        });
        var location = $"{Routes.Deals}/{deal.Id}";

        // Act — one attribute and one relationship; everything else keeps its value.
        var patched = await Client.PatchJsonApiAsync(location,
            Document.Patch(ResourceTypes.Deals, deal.Id, new { stage = "qualified" },
                (Rel.Contact, ResourceTypes.Contacts, contact.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
        deal.Stage = "qualified";
        deal.ContactId = contact.Id;
        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(Document.Deal(deal)));
    }

    [Fact]
    public async Task Patch_JsonApiNullContactLinkage_ClearsContact()
    {
        // Arrange — a deal that has a contact.
        var deal = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return db.Deals.Add(Rows.Deal("Losing its contact", company, Rows.User(),
                contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
        });
        var location = $"{Routes.Deals}/{deal.Id}";

        // Act — a null target id emits the clearing "data": null linkage.
        var patched = await Client.PatchJsonApiAsync(location,
            Document.Patch(ResourceTypes.Deals, deal.Id, attributes: null,
                (Rel.Contact, ResourceTypes.Contacts, null)));

        // Assert — the contact relationship is gone from the resource document.
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
        deal.ContactId = null;
        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(Document.Deal(deal)));
    }

    /// <summary>Referencing a resource that does not exist is a 404 per the JSON:API spec
    /// (https://jsonapi.org/format/#crud-updating-responses-404), distinct from the 422s for
    /// malformed data.</summary>
    [Fact]
    public async Task Patch_UnknownCompany_Returns404()
    {
        // Arrange
        var deal = await ArrangeAsync(db =>
            db.Deals.Add(Rows.Deal("Repointing at nothing", Rows.Company(), Rows.User())).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}",
            Document.Patch(ResourceTypes.Deals, deal.Id, attributes: null,
                (Rel.Company, ResourceTypes.Companies, 99999)));

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Company '99999' does not exist."));
    }

    [Fact]
    public async Task Patch_UnknownId_Returns404()
    {
        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/99999",
            Document.Patch(ResourceTypes.Deals, 99999, new { stage = "won" }));

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Deal '99999' does not exist."));
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        // Act
        var response = await Client.DeleteAsync($"{Routes.Deals}/99999");

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Deal '99999' does not exist."));
    }

    [Fact]
    public async Task PatchRelationship_Contact_SetThenClear_UpdatesLinkage()
    {
        // Arrange — a deal without a contact and a contact to point it at.
        var (deal, contact) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return (db.Deals.Add(Rows.Deal("Needs a contact", company, Rows.User())).Entity,
                db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity);
        });
        var linkageUrl = $"{Routes.Deals}/{deal.Id}/relationships/contact";
        var relatedUrl = $"{Routes.Deals}/{deal.Id}/contact";

        // Act + Assert — set: the linkage endpoint reports the new identifier.
        var set = await Client.PatchJsonApiAsync(linkageUrl,
            Document.Linkage((ResourceTypes.Contacts, contact.Id)));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        var linkage = await Client.GetDocumentAsync(linkageUrl);
        linkage.ShouldMatchExactly(Document.Linkage(linkageUrl, relatedUrl, (ResourceTypes.Contacts, contact.Id)));

        // Act + Assert — clear: back to explicit null linkage.
        var cleared = await Client.PatchJsonApiAsync(linkageUrl, Document.Linkage(null));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        var emptied = await Client.GetDocumentAsync(linkageUrl);
        emptied.ShouldMatchExactly(Document.Linkage(linkageUrl, relatedUrl, identifier: null));
    }

    [Fact]
    public async Task PatchRelationship_Contact_WrongType_Returns409()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act — a users identifier where the relationship holds contacts.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}/relationships/contact",
            Document.Linkage((ResourceTypes.Users, 1)));

        // Assert
        var problem = await response.ReadProblemAsync(409);
        problem.ShouldMatchExactly(Document.Problem(409, "Conflict",
            "This relationship expects resources of type 'contacts', got 'users'."));
    }

    [Fact]
    public async Task PatchRelationship_Contact_UnknownTarget_Returns404()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}/relationships/contact",
            Document.Linkage((ResourceTypes.Contacts, 99999)));

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Contact '99999' does not exist."));
    }

    [Fact]
    public async Task PatchRelationship_Contact_MissingDataMember_Returns400()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act — not a linkage document at all.
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}/relationships/contact",
            new { contactId = 5 });

        // Assert
        var problem = await response.ReadProblemAsync(400);
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid relationship document",
            "The request body must be a JSON:API to-one linkage document with a 'data' member."));
    }

    /// <summary>Every deal belongs to a company, so the to-one clear form (data: null) is rejected
    /// rather than leaving an orphaned deal.</summary>
    [Fact]
    public async Task PatchRelationship_Company_ClearingRequired_Returns422()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Deals}/{deal.Id}/relationships/company",
            Document.Linkage(null));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "The 'company' relationship is required and cannot be cleared."));
    }

    [Fact]
    public async Task PatchRelationship_Owner_Set_UpdatesLinkage()
    {
        // Arrange — a second user to hand the deal to.
        var (deal, newOwner) = await ArrangeAsync(db =>
            (db.Deals.Add(Rows.Deal("Handover", Rows.Company(), Rows.User("Sarah Chen"))).Entity,
                db.Users.Add(Rows.User("Marcus Webb")).Entity));
        var linkageUrl = $"{Routes.Deals}/{deal.Id}/relationships/owner";

        // Act
        var response = await Client.PatchJsonApiAsync(linkageUrl,
            Document.Linkage((ResourceTypes.Users, newOwner.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var linkage = await Client.GetDocumentAsync(linkageUrl);
        linkage.ShouldMatchExactly(Document.Linkage(linkageUrl,
            $"{Routes.Deals}/{deal.Id}/owner", (ResourceTypes.Users, newOwner.Id)));
    }

    /// <summary>Amounts chosen so numeric and lexical ordering disagree in both directions:
    /// 8000 &lt; 12500 &lt; 48000 numerically, but "12500" &lt; "48000" &lt; "8000" as strings.</summary>
    private void DealsWithTellingAmounts(AppDbContext db)
    {
        var company = Rows.Company();
        var owner = Rows.User();
        _biggest = db.Deals.Add(Rows.Deal("Biggest", company, owner, amount: 48000m)).Entity;
        _smallest = db.Deals.Add(Rows.Deal("Smallest", company, owner, amount: 8000m)).Entity;
        _middle = db.Deals.Add(Rows.Deal("Middle", company, owner, amount: 12500m)).Entity;
    }

    private static List<Deal> AddDeals(AppDbContext db, int count)
    {
        var company = Rows.Company();
        var owner = Rows.User();
        var deals = new List<Deal>();
        for (var i = 1; i <= count; i++)
        {
            deals.Add(db.Deals.Add(Rows.Deal($"Deal {i}", company, owner)).Entity);
        }
        return deals;
    }

    private static Deal DealWithoutContact(AppDbContext db) =>
        db.Deals.Add(Rows.Deal("Hardware upgrade", Rows.Company(), Rows.User())).Entity;
}
