using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ContactEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Contact _jan = null!;
    private Contact _maria = null!;
    private Contact _adam = null!;

    [Fact]
    public async Task List_Default_ReturnsArrangedCollection()
    {
        // Arrange
        var (jan, maria) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            return (
                db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity,
                db.Contacts.Add(Rows.Contact("Maria", "Nowak", company)).Entity);
        });

        // Act
        var document = await Client.GetDocumentAsync(Routes.Contacts);

        // Assert — the entire document: both contacts with attributes, company relationship
        // (links + linkage), links.self each, pagination links, meta.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                null,
                1, 25, total: 2,
                [Document.Contact(jan), Document.Contact(maria)]));
    }

    [Fact]
    public async Task List_IncludeCompany_SideloadsCompanies()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var query = $"include={Rel.Company}";
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}?{query}");

        // Assert — the compound document sideloads exactly the one company.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                query,
                1, 25, total: 1,
                [Document.Contact(contact)],
                Document.Company(contact.Company!)));
    }

    [Fact]
    public async Task List_IncludeWithSparseFieldset_LimitsIncludedAttributes()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var query = $"include={Rel.Company}&fields[{ResourceTypes.Companies}]={Attr.Name}";
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}?{query}");

        // Assert — the fieldset trims the included company to its name (and drops its contacts
        // relationship, a spec "field"); the primary contacts keep their full shape.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                query,
                1, 25, total: 1,
                [Document.Contact(contact)],
                Document.Company(contact.Company!).Fields(Attr.Name)));
    }

    [Fact]
    public async Task List_SortByLastNameDescending_OrdersCorrectly()
    {
        // Arrange
        await ArrangeAsync(TwoKowalskisAndANowak);

        // Act
        var query = $"sort=-{Attr.LastName}";
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}?{query}");

        // Assert — Nowak sorts after Kowalski, so descending puts it first; the tied Kowalskis
        // follow in id order.
        var kowalskis = new[] { _jan, _adam }.OrderBy(c => c.Id).ToArray();
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                query,
                1, 25, total: 3,
                [Document.Contact(_maria), Document.Contact(kowalskis[0]), Document.Contact(kowalskis[1])]));
    }

    [Fact]
    public async Task List_SortByLastNameThenFirstName_BreaksTieWithinSharedLastName()
    {
        // Arrange
        await ArrangeAsync(TwoKowalskisAndANowak);

        // Act
        var query = $"sort={Attr.LastName},{Attr.FirstName}";
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}?{query}");

        // Assert — the shared last name is broken by first name ascending: Adam, Jan, Maria.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                query,
                1, 25, total: 3,
                [Document.Contact(_adam), Document.Contact(_jan), Document.Contact(_maria)]));
    }

    [Fact]
    public async Task List_SortTermsMixDirections_AppliesEachDirectionIndependently()
    {
        // Arrange
        await ArrangeAsync(TwoKowalskisAndANowak);

        // Act
        var query = $"sort={Attr.LastName},-{Attr.FirstName}";
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}?{query}");

        // Assert — lastName still ascending (both Kowalskis lead), but '-' flips the tie-break.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Contacts,
                query,
                1, 25, total: 3,
                [Document.Contact(_jan), Document.Contact(_adam), Document.Contact(_maria)]));
    }

    [Fact]
    public async Task GetById_ContactWithCustomFields_ReturnsTypedValues()
    {
        // Arrange — string-stored values whose declared types are text and boolean.
        var contact = await ArrangeAsync(db =>
        {
            var jan = Rows.Contact("Jan", "Kowalski", Rows.Company());
            var leadSource = Rows.Field(ResourceTypes.Contacts, Attr.LeadSource);
            var newsletter = Rows.Field(ResourceTypes.Contacts, Attr.NewsletterOptIn, dataType: "boolean");
            db.AddRange(jan, leadSource, newsletter);
            db.SaveChanges(); // assigns the ids the value store references

            db.CustomFieldValues.AddRange(
                Rows.Value(leadSource, jan.Id, "referral"),
                Rows.Value(newsletter, jan.Id, "true"));
            return jan;
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}/{contact.Id}");

        // Assert — the boolean comes back as JSON true, not the stored string "true".
        document.ShouldMatchExactly(Document.Single(
            Document.Contact(contact, customFields: new { leadSource = "referral", newsletterOptIn = true })));
    }

    [Fact]
    public async Task GetById_ContactWithFalseBoolean_ReturnsJsonFalse()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
        {
            var maria = Rows.Contact("Maria", "Nowak", Rows.Company());
            var newsletter = Rows.Field(ResourceTypes.Contacts, Attr.NewsletterOptIn, dataType: "boolean");
            db.AddRange(maria, newsletter);
            db.SaveChanges();

            db.CustomFieldValues.Add(Rows.Value(newsletter, maria.Id, "false"));
            return maria;
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}/{contact.Id}");

        // Assert — false must survive the string value store as JSON false, not "false" or absent.
        document.ShouldMatchExactly(Document.Single(
            Document.Contact(contact, customFields: new { newsletterOptIn = false })));
    }

    [Fact]
    public async Task GetRelated_Company_ReturnsFullResource()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company("Acme Manufacturing"))).Entity);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}/{contact.Id}/company");

        // Assert — the full company resource plus the document's self link.
        document.ShouldMatchExactly(Document.Related(
            $"{Routes.Contacts}/{contact.Id}/company", Document.Company(contact.Company!)));
    }

    [Fact]
    public async Task GetRelationship_Company_ReturnsLinkageOnly()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Contacts}/{contact.Id}/relationships/company");

        // Assert — exactly the identifier and the two links; no attributes anywhere.
        document.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Contacts}/{contact.Id}/relationships/company",
            $"{Routes.Contacts}/{contact.Id}/company",
            (ResourceTypes.Companies, contact.CompanyId)));
    }

    [Fact]
    public async Task Lifecycle_CreatePatchDelete_Succeeds()
    {
        // Arrange — the company the new contact belongs to, and the field the write references.
        var company = await ArrangeAsync(db =>
        {
            db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Contacts, Attr.LeadSource));
            return db.Companies.Add(Rows.Company()).Entity;
        });

        // Act + Assert — create: 201 with Location and the full created resource as the body.
        var created = await Client.PostJsonApiAsync(Routes.Contacts, Document.Post(ResourceTypes.Contacts,
            new
            {
                firstName = "Tomasz",
                lastName = "Lis",
                email = "tomasz.lis@acme.example.com",
                phone = "+48 500 100 200",
                customFields = new { leadSource = "conference" }
            },
            (Rel.Company, ResourceTypes.Companies, company.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(JsonApiMediaTypes.JsonApi, created.Content.Headers.ContentType?.MediaType);

        var location = created.Headers.Location!.ToString();
        var body = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = int.Parse(body[Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal($"{Routes.Contacts}/{id}", location);

        var tomasz = new Contact
        {
            Id = id, FirstName = "Tomasz", LastName = "Lis",
            Email = "tomasz.lis@acme.example.com", Phone = "+48 500 100 200", CompanyId = company.Id
        };
        body.ShouldMatchExactly(Document.Single(
            Document.Contact(tomasz, customFields: new { leadSource = "conference" })));

        // Act + Assert — patch: only provided fields change.
        var patched = await Client.PatchJsonApiAsync(location,
            Document.Patch(ResourceTypes.Contacts, id, new { email = "t.lis@acme.example.com" }));
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);

        tomasz.Email = "t.lis@acme.example.com";
        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(
            Document.Contact(tomasz, customFields: new { leadSource = "conference" })));

        // Act + Assert — delete: subsequent GET is a 404.
        var deleted = await Client.DeleteAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Post_JsonApiResourceDocument_CreatesContact()
    {
        // Arrange
        var company = await ArrangeAsync(db => db.Companies.Add(Rows.Company()).Entity);

        // Act — a create with every attribute and the company relationship.
        var created = await Client.PostJsonApiAsync(Routes.Contacts, Document.Post(ResourceTypes.Contacts,
            new
            {
                firstName = "Tomasz",
                lastName = "Lis",
                email = "tomasz.lis@acme.example.com",
                phone = "+48 500 100 200"
            },
            (Rel.Company, ResourceTypes.Companies, company.Id)));

        // Assert — 201 with Location and the full created resource.
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        var body = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = int.Parse(body[Doc.Data]![Doc.Id]!.GetValue<string>());
        Assert.Equal($"{Routes.Contacts}/{id}", location);

        var tomasz = new Contact
        {
            Id = id, FirstName = "Tomasz", LastName = "Lis",
            Email = "tomasz.lis@acme.example.com", Phone = "+48 500 100 200", CompanyId = company.Id
        };
        body.ShouldMatchExactly(Document.Single(Document.Contact(tomasz)));
    }

    [Fact]
    public async Task Patch_JsonApiResourceDocument_UpdatesAttributesAndRepointsCompany()
    {
        // Arrange — a contact and the second company the patch moves it to.
        var (contact, newCompany) = await ArrangeAsync(db =>
            (db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity,
                db.Companies.Add(Rows.Company("Zephyr Labs", "Software")).Entity));
        var location = $"{Routes.Contacts}/{contact.Id}";

        // Act — one attribute and the company relationship; everything else keeps its value.
        var patched = await Client.PatchJsonApiAsync(location,
            Document.Patch(ResourceTypes.Contacts, contact.Id, new { email = "jan.kowalski@zephyr.example.com" },
                (Rel.Company, ResourceTypes.Companies, newCompany.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
        contact.Email = "jan.kowalski@zephyr.example.com";
        contact.CompanyId = newCompany.Id;
        var reloaded = await Client.GetDocumentAsync(location);
        reloaded.ShouldMatchExactly(Document.Single(Document.Contact(contact)));
    }

    [Fact]
    public async Task Post_MissingRequiredFields_Returns422()
    {
        // Act
        var response = await Client.PostJsonApiAsync(Routes.Contacts,
            Document.Post(ResourceTypes.Contacts, new { email = "nobody@example.com" }));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "The 'firstName' and 'lastName' fields are required."));
    }

    /// <summary>The API's only write contract is JSON:API; a flat application/json body is
    /// refused up front by the content negotiation middleware.</summary>
    [Fact]
    public async Task Post_FlatJsonContentType_Returns415()
    {
        // Act
        var response = await Client.PostAsJsonAsync(Routes.Contacts, new { firstName = "Flat" });

        // Assert
        var problem = await response.ReadProblemAsync(415);
        problem.ShouldMatchExactly(Document.Problem(415, "Unsupported media type",
            "This API accepts only JSON:API request bodies; send the payload as 'application/vnd.api+json'."));
    }

    /// <summary>Referencing a resource that does not exist is a 404 per the JSON:API spec
    /// (https://jsonapi.org/format/#crud-creating-responses-404), distinct from the 422s for
    /// malformed data.</summary>
    [Fact]
    public async Task Post_UnknownCompany_Returns404()
    {
        // Act
        var response = await Client.PostJsonApiAsync(Routes.Contacts,
            Document.Post(ResourceTypes.Contacts, new { firstName = "Ola", lastName = "Mazur" },
                (Rel.Company, ResourceTypes.Companies, 99999)));

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found",
            "Company '99999' does not exist."));
    }

    [Fact]
    public async Task Post_UnknownCustomField_Returns422()
    {
        // Arrange
        var company = await ArrangeAsync(db => db.Companies.Add(Rows.Company()).Entity);

        // Act
        var response = await Client.PostJsonApiAsync(Routes.Contacts,
            Document.Post(ResourceTypes.Contacts,
                new
                {
                    firstName = "Ewa",
                    lastName = "Kot",
                    customFields = new { bogusField = "x" }
                },
                (Rel.Company, ResourceTypes.Companies, company.Id)));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "Unknown custom field 'bogusField' for resource type 'contacts'."));
    }

    /// <inheritdoc cref="Post_UnknownCompany_Returns404"/>
    [Fact]
    public async Task Patch_UnknownCompany_Returns404()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Contacts}/{contact.Id}",
            Document.Patch(ResourceTypes.Contacts, contact.Id, attributes: null,
                (Rel.Company, ResourceTypes.Companies, 99999)));

        // Assert
        var problem = await response.ReadProblemAsync(404);
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Company '99999' does not exist."));
    }

    [Fact]
    public async Task Patch_UnknownId_Returns404()
    {
        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Contacts}/99999",
            Document.Patch(ResourceTypes.Contacts, 99999, new { email = "x@example.com" }));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_EmptyFirstName_Returns422()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Contacts}/{contact.Id}",
            Document.Patch(ResourceTypes.Contacts, contact.Id, new { firstName = "" }));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "The 'firstName' and 'lastName' fields cannot be empty."));
    }

    [Fact]
    public async Task PatchRelationship_Company_Set_UpdatesLinkage()
    {
        // Arrange — a contact employed at one company and a second company to move them to.
        var (contact, newCompany) = await ArrangeAsync(db =>
            (db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity,
                db.Companies.Add(Rows.Company("Globex Retail", "Retail")).Entity));
        var linkageUrl = $"{Routes.Contacts}/{contact.Id}/relationships/company";

        // Act
        var response = await Client.PatchJsonApiAsync(linkageUrl,
            Document.Linkage((ResourceTypes.Companies, newCompany.Id)));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var linkage = await Client.GetDocumentAsync(linkageUrl);
        linkage.ShouldMatchExactly(Document.Linkage(linkageUrl,
            $"{Routes.Contacts}/{contact.Id}/company", (ResourceTypes.Companies, newCompany.Id)));
    }

    /// <summary>Every contact belongs to a company, so the to-one clear form (data: null) is
    /// rejected rather than leaving an orphaned contact.</summary>
    [Fact]
    public async Task PatchRelationship_Company_ClearingRequired_Returns422()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var response = await Client.PatchJsonApiAsync($"{Routes.Contacts}/{contact.Id}/relationships/company",
            Document.Linkage(null));

        // Assert
        var problem = await response.ReadProblemAsync(422);
        problem.ShouldMatchExactly(Document.Problem(422, "Validation failed",
            "The 'company' relationship is required and cannot be cleared."));
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        // Act
        var response = await Client.DeleteAsync($"{Routes.Contacts}/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ContactReferencedByDeal_NullsTheDealRelationship()
    {
        // Arrange — a contact and a deal that points at them.
        var (contact, deal) = await ArrangeAsync(db =>
        {
            var karol = Rows.Contact("Karol", "Wrona", Rows.Company());
            var karolsDeal = Rows.Deal("Karol's deal", karol.Company!, Rows.User(), contact: karol);
            db.Deals.Add(karolsDeal);
            return (karol, karolsDeal);
        });

        var linkage = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/relationships/contact");
        linkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Deals}/{deal.Id}/relationships/contact",
            $"{Routes.Deals}/{deal.Id}/contact",
            (ResourceTypes.Contacts, contact.Id)));

        // Act — deleting the contact must not delete the deal, only null the relationship.
        var deleted = await Client.DeleteAsync($"{Routes.Contacts}/{contact.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var orphaned = await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}/relationships/contact");
        orphaned.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Deals}/{deal.Id}/relationships/contact",
            $"{Routes.Deals}/{deal.Id}/contact", identifier: null));
    }

    /// <summary>Two contacts share a last name, so sorting by lastName alone cannot order them.</summary>
    private void TwoKowalskisAndANowak(AppDbContext db)
    {
        var company = Rows.Company();
        _jan = db.Contacts.Add(Rows.Contact("Jan", "Kowalski", company)).Entity;
        _maria = db.Contacts.Add(Rows.Contact("Maria", "Nowak", company)).Entity;
        _adam = db.Contacts.Add(Rows.Contact("Adam", "Kowalski", company)).Entity;
    }
}
