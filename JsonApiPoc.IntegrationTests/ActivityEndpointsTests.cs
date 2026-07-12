using System.Net.Http.Json;

namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ActivityEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>Relationship updates are refused wholesale with the spec-required 403
    /// (https://jsonapi.org/format/#crud-updating-relationship-responses-403).</summary>
    [Fact]
    public async Task PatchRelationship_Deal_Returns403()
    {
        // Act
        var response = await Client.PatchAsJsonAsync($"{Routes.Activities}/1/relationships/deal",
            new { data = (object?)null });

        // Assert
        var problem = await response.ReadProblemAsync(403);
        problem.ShouldMatchExactly(Document.Problem(403, "Forbidden",
            "This API does not support updating relationships."));
    }

    [Fact]
    public async Task List_IncludeDealAndContact_SideloadsBoth()
    {
        // Arrange — one activity with both relationships, one standalone.
        var (call, cleanup) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var contact = Rows.Contact("Jan", "Kowalski", company);
            var deal = Rows.Deal("ERP integration", company, Rows.User());
            return (
                db.Activities.Add(Rows.Activity("Discovery call", kind: "call", deal: deal, contact: contact)).Entity,
                db.Activities.Add(Rows.Activity("Standalone cleanup")).Entity);
        });

        // Act
        var query = $"include={Rel.Deal},{Rel.Contact}";
        var document = await Client.GetDocumentAsync($"{Routes.Activities}?{query}");

        // Assert — the full compound document: both activities in id order (EF inserts the
        // dependency-free row first, so ids don't follow Add order), and exactly the linked deal
        // and contact sideloaded (deals before contacts).
        var inIdOrder = new[] { call, cleanup }.OrderBy(a => a.Id)
            .Select(Document.Activity).ToArray();
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Activities,
                query,
                1, 25, total: 2,
                inIdOrder,
                Document.Deal(call.Deal!),
                Document.Contact(call.Contact!)));
    }

    [Fact]
    public async Task List_SortByDueDate_PutsEarliestFirst()
    {
        // Arrange — inserted out of order, so passing means the endpoint sorted rather than echoed.
        var (last, first, second) = await ArrangeAsync(db => (
            db.Activities.Add(Rows.Activity("Due last", dueAt: new DateTime(2026, 3, 1))).Entity,
            db.Activities.Add(Rows.Activity("Due first", dueAt: new DateTime(2026, 1, 1))).Entity,
            db.Activities.Add(Rows.Activity("Due second", dueAt: new DateTime(2026, 2, 1))).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}?sort=dueAt");

        // Assert
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Activities,
                "sort=dueAt",
                1, 25, total: 3,
                [Document.Activity(first), Document.Activity(second), Document.Activity(last)]));
    }

    [Fact]
    public async Task List_SortByKind_OrdersAlphabetically()
    {
        // Arrange — inserted in reverse of the expected order.
        var (task, meeting, email, call) = await ArrangeAsync(db => (
            db.Activities.Add(Rows.Activity("A task", kind: "task")).Entity,
            db.Activities.Add(Rows.Activity("A meeting", kind: "meeting")).Entity,
            db.Activities.Add(Rows.Activity("An email", kind: "email")).Entity,
            db.Activities.Add(Rows.Activity("A call", kind: "call")).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}?sort={Attr.Kind}");

        // Assert — call < email < meeting < task.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Activities,
                $"sort={Attr.Kind}",
                1, 25, total: 4,
                [Document.Activity(call), Document.Activity(email), Document.Activity(meeting), Document.Activity(task)]));
    }

    [Fact]
    public async Task List_SortByKind_BreaksTiesByIdSoPagingStaysStable()
    {
        // Arrange — two rows share a kind, so the sort key alone cannot order them.
        var (firstTask, call, secondTask) = await ArrangeAsync(db => (
            db.Activities.Add(Rows.Activity("First task", kind: "task")).Entity,
            db.Activities.Add(Rows.Activity("A call", kind: "call")).Entity,
            db.Activities.Add(Rows.Activity("Second task", kind: "task")).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}?sort={Attr.Kind}");

        // Assert — the call leads; the tied tasks follow in ascending-id order (the tie-break).
        var tied = new[] { firstTask, secondTask }.OrderBy(a => a.Id).ToArray();
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Activities,
                $"sort={Attr.Kind}",
                1, 25, total: 3,
                [Document.Activity(call), Document.Activity(tied[0]), Document.Activity(tied[1])]));
    }

    [Fact]
    public async Task GetRelationships_StandaloneTask_ReturnsNullDataForBoth()
    {
        // Arrange — an activity tied to neither a deal nor a contact.
        var activity = await ArrangeAsync(db => db.Activities.Add(Rows.Activity("Standalone cleanup")).Entity);

        // Act
        var dealLinkage = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}/relationships/deal");
        var contactLinkage = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}/relationships/contact");

        // Assert — full linkage documents: explicit data:null plus self/related links.
        dealLinkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Activities}/{activity.Id}/relationships/deal",
            $"{Routes.Activities}/{activity.Id}/deal", identifier: null));
        contactLinkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Activities}/{activity.Id}/relationships/contact",
            $"{Routes.Activities}/{activity.Id}/contact", identifier: null));
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsResource()
    {
        // Arrange
        var activity = await ArrangeAsync(db =>
            db.Activities.Add(Rows.Activity("Discovery call", kind: "call", completed: true)).Entity);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}");

        // Assert — the whole document: identity, every attribute, links.self, nothing else.
        document.ShouldMatchExactly(Document.Single(Document.Activity(activity)));
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Activities}/99999", 404);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "Activity '99999' does not exist."));
    }

    [Fact]
    public async Task GetRelationship_ActivityWithoutContact_ReturnsNullData()
    {
        // Arrange — the activity has a deal but no contact.
        var activity = await ArrangeAsync(db =>
        {
            var deal = Rows.Deal("ERP integration", Rows.Company(), Rows.User());
            return db.Activities.Add(Rows.Activity("Send proposal", kind: "email", deal: deal)).Entity;
        });

        // Act
        var linkage = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}/relationships/contact");

        // Assert
        linkage.ShouldMatchExactly(Document.Linkage(
            $"{Routes.Activities}/{activity.Id}/relationships/contact",
            $"{Routes.Activities}/{activity.Id}/contact", identifier: null));
    }

    [Fact]
    public async Task GetRelated_Deal_ReturnsFullResource()
    {
        // Arrange
        var activity = await ArrangeAsync(db =>
        {
            var deal = Rows.Deal("Fleet tracking pilot", Rows.Company(), Rows.User());
            return db.Activities.Add(Rows.Activity("Pilot kickoff", kind: "meeting", deal: deal)).Entity;
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}/deal");

        // Assert — the arranged deal as a full resource (attributes, relationships, links) plus
        // the document's own self link.
        document.ShouldMatchExactly(Document.Related(
            $"{Routes.Activities}/{activity.Id}/deal", Document.Deal(activity.Deal!)));
    }

    [Fact]
    public async Task GetRelated_Contact_ReturnsContactWhenSet()
    {
        // Arrange
        var activity = await ArrangeAsync(db =>
        {
            var contact = Rows.Contact("Piotr", "Wisniewski", Rows.Company());
            return db.Activities.Add(Rows.Activity("Pilot kickoff", kind: "meeting", contact: contact)).Entity;
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Activities}/{activity.Id}/contact");

        // Assert
        document.ShouldMatchExactly(Document.Related(
            $"{Routes.Activities}/{activity.Id}/contact", Document.Contact(activity.Contact!)));
    }
}