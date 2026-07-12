using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CompanyEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task List_Default_ReturnsArrangedCollection()
    {
        // Arrange
        var (acme, globex) = await ArrangeAsync(db => (
            db.Companies.Add(Rows.Company("Acme Manufacturing")).Entity,
            db.Companies.Add(Rows.Company("Globex Logistics")).Entity));

        // Act — GetDocumentAsync asserts the 200 and the JSON:API media type.
        var document = await Client.GetDocumentAsync(Routes.Companies);

        // Assert — the entire document: both resources in id order, pagination links, meta.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                null,
                1, 25, total: 2,
                [Document.Company(acme), Document.Company(globex)]));
    }

    [Fact]
    public async Task List_SortByName_OrdersBothDirections()
    {
        // Arrange — inserted in neither ascending nor descending order.
        var (globex, umbrella, acme) = await ArrangeAsync(db => (
            db.Companies.Add(Rows.Company("Globex Logistics")).Entity,
            db.Companies.Add(Rows.Company("Umbrella Health")).Entity,
            db.Companies.Add(Rows.Company("Acme Manufacturing")).Entity));

        // Act
        var ascending = await Client.GetDocumentAsync($"{Routes.Companies}?sort={Attr.Name}");
        var descending = await Client.GetDocumentAsync($"{Routes.Companies}?sort=-{Attr.Name}");

        // Assert — full collections; one direction must be the exact reverse of the other.
        ascending.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                $"sort={Attr.Name}",
                1, 25, total: 3,
                [Document.Company(acme), Document.Company(globex), Document.Company(umbrella)]));
        descending.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                $"sort=-{Attr.Name}",
                1, 25, total: 3,
                [Document.Company(umbrella), Document.Company(globex), Document.Company(acme)]));
    }

    [Fact]
    public async Task List_SortByIndustry_OrdersAlphabetically()
    {
        // Arrange — industry order disagrees with name order, so passing means the right field sorted.
        var (acme, umbrella) = await ArrangeAsync(db => (
            db.Companies.Add(Rows.Company("Acme Manufacturing", industry: "Manufacturing")).Entity,
            db.Companies.Add(Rows.Company("Umbrella Health", industry: "Healthcare")).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Companies}?sort={Attr.Industry}");

        // Assert — Healthcare sorts first even though Umbrella is alphabetically the last name.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                $"sort={Attr.Industry}",
                1, 25, total: 2,
                [Document.Company(umbrella), Document.Company(acme)]));
    }

    [Fact]
    public async Task List_SparseFieldset_LimitsAttributes()
    {
        // Arrange
        var company = await ArrangeAsync(db => db.Companies.Add(Rows.Company()).Entity);

        // Act
        var query = $"fields[{ResourceTypes.Companies}]={Attr.Name}";
        var document = await Client.GetDocumentAsync($"{Routes.Companies}?{query}");

        // Assert — only the name attribute survives; the contacts relationship (a spec "field")
        // is filtered out too, while links.self stays.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                query,
                1, 25, total: 1,
                [Document.Company(company).Fields(Attr.Name)]));
    }

    [Fact]
    public async Task List_PageSizeOne_PaginatesWithPrevAndNextLinks()
    {
        // Arrange — three rows at page size 1 put page 2 between two neighbours.
        var (_, globex, _) = await ArrangeAsync(db => (
            db.Companies.Add(Rows.Company("Acme Manufacturing")).Entity,
            db.Companies.Add(Rows.Company("Globex Logistics")).Entity,
            db.Companies.Add(Rows.Company("Umbrella Health")).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Companies}?page[size]=1&page[number]=2");

        // Assert — page 2 of 3 holds exactly the middle company, and the full link set (self,
        // first, prev, next, last) points where it should.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Companies,
                null,
                number: 2, size: 1, total: 3,
                [Document.Company(globex)]));
    }

    [Fact]
    public async Task List_UnknownSortField_Returns400()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Companies}?sort=height", 400);

        // Assert — the detail lists the supported fields.
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid sort",
            "Unsupported sort field 'height'. Supported fields: name, industry."));
    }

    [Fact]
    public async Task List_UnknownQueryParameter_Returns400()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Companies}?foo=1", 400);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid query parameter",
            "Unknown query parameter 'foo'."));
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsFullResource()
    {
        // Arrange
        var company = await ArrangeAsync(db =>
            db.Companies.Add(Rows.Company("Globex Logistics", industry: "Transportation")).Entity);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Companies}/{company.Id}");

        // Assert — the whole document: every attribute, the links-only contacts relationship
        // (related link, no linkage data, no self), links.self, and nothing else.
        document.ShouldMatchExactly(Document.Single(Document.Company(company)));
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404Problem()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Companies}/99999", 404);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Company '99999' does not exist."));
    }

    [Fact]
    public async Task GetContacts_CompanyWithContacts_ReturnsOnlyItsOwn()
    {
        // Arrange — two contacts at one company, a third elsewhere that must not leak in.
        var (company, jan, maria) = await ArrangeAsync(db =>
        {
            var acme = Rows.Company("Acme Manufacturing");
            var mariaRow = db.Contacts.Add(Rows.Contact("Maria", "Nowak", acme)).Entity;
            var janRow = db.Contacts.Add(Rows.Contact("Jan", "Kowalski", acme)).Entity;
            db.Contacts.Add(Rows.Contact("Piotr", "Wisniewski", Rows.Company("Globex Logistics")));
            return (acme, janRow, mariaRow);
        });

        // Act
        var path = $"{Routes.Companies}/{company.Id}/contacts";
        var document = await Client.GetDocumentAsync($"{path}?sort={Attr.LastName}");

        // Assert — only the company's own contacts, ordered by last name; pagination links keep
        // the nested path and the sort.
        document.ShouldMatchExactly(
            Document.Page(
                path,
                $"sort={Attr.LastName}",
                1, 25, total: 2,
                [Document.Contact(jan), Document.Contact(maria)]));
    }

    [Fact]
    public async Task GetContacts_CompanyWithoutContacts_ReturnsEmptyCollection()
    {
        // Arrange
        var company = await ArrangeAsync(db => db.Companies.Add(Rows.Company("Umbrella Health")).Entity);

        // Act
        var path = $"{Routes.Companies}/{company.Id}/contacts";
        var document = await Client.GetDocumentAsync(path);

        // Assert — an existing company with no contacts is an empty page, not a 404; first/last
        // links still exist because an empty collection has one (empty) page.
        document.ShouldMatchExactly(
            Document.Page(
                path,
                null,
                1, 25, total: 0,
                []));
    }

    [Fact]
    public async Task GetContacts_UnknownCompany_Returns404Problem()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Companies}/99999/contacts", 404);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Company '99999' does not exist."));
    }

    [Fact]
    public async Task GetContacts_PageSizeOne_PaginatesWithinTheCompany()
    {
        // Arrange
        var (company, jan) = await ArrangeAsync(db =>
        {
            var acme = Rows.Company();
            var janRow = db.Contacts.Add(Rows.Contact("Jan", "Kowalski", acme)).Entity;
            db.Contacts.Add(Rows.Contact("Maria", "Nowak", acme));
            return (acme, janRow);
        });

        // Act
        var path = $"{Routes.Companies}/{company.Id}/contacts";
        var document = await Client.GetDocumentAsync($"{path}?page[size]=1");

        // Assert — pagination links keep the nested path.
        document.ShouldMatchExactly(
            Document.Page(
                path,
                null,
                number: 1, size: 1, total: 2,
                [Document.Contact(jan)]));
    }

    [Fact]
    public async Task GetContacts_SparseFieldset_LimitsAttributes()
    {
        // Arrange
        var contact = await ArrangeAsync(db =>
            db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity);

        // Act
        var path = $"{Routes.Companies}/{contact.CompanyId}/contacts";
        var query = $"fields[{ResourceTypes.Contacts}]={Attr.LastName}";
        var document = await Client.GetDocumentAsync($"{path}?{query}");

        // Assert — exactly the lastName attribute; the company relationship is filtered out too.
        document.ShouldMatchExactly(
            Document.Page(
                path,
                query,
                1, 25, total: 1,
                [Document.Contact(contact).Fields(Attr.LastName)]));
    }

    [Fact]
    public async Task GetContacts_UnknownSortField_Returns400()
    {
        // Arrange
        var company = await ArrangeAsync(db => db.Companies.Add(Rows.Company()).Entity);

        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Companies}/{company.Id}/contacts?sort=height", 400);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid sort",
            "Unsupported sort field 'height'. Supported fields: lastName, firstName, email."));
    }
}
