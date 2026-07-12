using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>Checks the read endpoints against the normative RFC 2119 sentences of the JSON:API 1.1
/// "Fetching Data" section (https://jsonapi.org/format/#fetching). Each test quotes verbatim the
/// sentence it enforces. The theories run against every entry in <see cref="SpecResources"/>, so a
/// new endpoint joins the suite by adding one catalog entry. Assertions stay at the spec level —
/// status codes, member presence, and ordering — so a failure here means a conformance violation,
/// not a changed wire shape (the per-endpoint tests pin those).</summary>
[Collection(ApiCollection.Name)]
public class FetchingSpecComplianceTests(ApiFactory factory) : ApiTestBase(factory)
{
    // ── Fetching Resources (§ fetching-resources) ───────────────────────────────────────────────

    /// <summary>"A server MUST support fetching resource data for every URL provided as: a self
    /// link as part of the top-level links object, a self link as part of a resource-level links
    /// object, a related link as part of a relationship-level links object."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.GetByIdKeys), MemberType = typeof(SpecResources))]
    public async Task FetchingResources_EveryAdvertisedLink_SupportsFetching(string key)
    {
        // Arrange — one row with every relationship populated, so all links get advertised.
        var resource = SpecResources.Get(key);
        var id = IdOf(await ArrangeAsync(resource.ArrangeOne));

        // Act — collect every URL the API hands out on the resource and its collection.
        var single = await Client.GetDocumentAsync($"{resource.Route}/{id}");
        var collection = await Client.GetDocumentAsync(resource.Route);
        var urls = new List<string> { collection[Doc.Links]![Doc.Self]!.GetValue<string>() };
        if (single[Doc.Data]![Doc.Links] is JsonObject resourceLinks)
        {
            urls.Add(resourceLinks[Doc.Self]!.GetValue<string>());
        }
        if (single[Doc.Data]![Doc.Relationships] is JsonObject relationships)
        {
            foreach (var (_, relationship) in relationships)
            {
                var links = relationship![Doc.Links]!.AsObject();
                if (links.TryGetPropertyValue(Doc.Self, out var self))
                {
                    urls.Add(self!.GetValue<string>());
                }
                if (links.TryGetPropertyValue(Doc.Related, out var related))
                {
                    urls.Add(related!.GetValue<string>());
                }
            }
        }

        // Assert — every advertised URL is fetchable as JSON:API (GetDocumentAsync throws on a
        // non-success status or wrong media type).
        foreach (var url in urls)
        {
            await Client.GetDocumentAsync(url);
        }
    }

    /// <summary>"A server MUST respond to a successful request to fetch an individual resource or
    /// resource collection with a 200 OK response."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task FetchingResources_SuccessfulFetch_Returns200Ok(string key)
    {
        // Arrange
        var resource = SpecResources.Get(key);
        var id = IdOf(await ArrangeAsync(resource.ArrangeOne));

        // Act + Assert — exactly 200, not merely any success status.
        var collection = await Client.GetAsync(resource.Route);
        Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
        if (resource.HasGetById)
        {
            var individual = await Client.GetAsync($"{resource.Route}/{id}");
            Assert.Equal(HttpStatusCode.OK, individual.StatusCode);
        }
    }

    /// <summary>"A server MUST respond to a successful request to fetch a resource collection with
    /// an array of resource objects or an empty array ([]) as the response document's primary
    /// data." The empty collection is the telling case: data must be [], never null.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task FetchingResources_EmptyCollection_ReturnsEmptyDataArray(string key)
    {
        // Act — the database is empty; no arrange.
        var document = await Client.GetDocumentAsync(SpecResources.Get(key).Route);

        // Assert
        var data = Assert.IsType<JsonArray>(document[Doc.Data]);
        Assert.Empty(data);
    }

    /// <summary>"A server MUST respond to a successful request to fetch an individual resource
    /// with a resource object or null provided as the response document's primary data."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.GetByIdKeys), MemberType = typeof(SpecResources))]
    public async Task FetchingResources_IndividualResource_ReturnsResourceObjectPrimaryData(string key)
    {
        // Arrange
        var resource = SpecResources.Get(key);
        var id = IdOf(await ArrangeAsync(resource.ArrangeOne));

        // Act
        var document = await Client.GetDocumentAsync($"{resource.Route}/{id}");

        // Assert — primary data is a resource object identifying the requested resource.
        var data = Assert.IsType<JsonObject>(document[Doc.Data]);
        Assert.Equal(resource.Type, data[Doc.Type]!.GetValue<string>());
        Assert.Equal(id.ToString(), data[Doc.Id]!.GetValue<string>());
    }

    /// <summary>"null is only an appropriate response when the requested URL is one that might
    /// correspond to a single resource, but doesn't currently." — a related to-one URL whose
    /// target is empty must return 200 with an explicit data: null, not 404. Deal-specific: the
    /// deal→contact relationship is this API's nullable to-one.</summary>
    [Fact]
    public async Task FetchingResources_EmptyToOneRelatedUrl_Returns200WithNullData()
    {
        // Arrange — a deal that has no contact.
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var response = await Client.GetAsync($"{Routes.Deals}/{deal.Id}/contact");

        // Assert — 200 and a present-but-null data member.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(document.TryGetPropertyValue(Doc.Data, out var data));
        Assert.Null(data);
    }

    /// <summary>"A server MUST respond with 404 Not Found when processing a request to fetch a
    /// single resource that does not exist."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.GetByIdKeys), MemberType = typeof(SpecResources))]
    public async Task FetchingResources_UnknownSingleResource_Returns404(string key)
    {
        // Act + Assert — GetProblemAsync throws unless the status is 404 with a problem body.
        await Client.GetProblemAsync($"{SpecResources.Get(key).Route}/99999", 404);
    }

    // ── Fetching Relationships (§ fetching-relationships) ───────────────────────────────────────

    /// <summary>"A server MUST respond to a successful request to fetch a relationship with a 200
    /// OK response." and "The primary data in the response document MUST match the appropriate
    /// value for resource linkage" — checked for every relationship self link the resource
    /// advertises.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.GetByIdKeys), MemberType = typeof(SpecResources))]
    public async Task FetchingRelationships_EveryRelationshipSelfLink_Returns200WithMatchingLinkage(string key)
    {
        // Arrange
        var resource = SpecResources.Get(key);
        var id = IdOf(await ArrangeAsync(resource.ArrangeOne));
        var parent = await Client.GetDocumentAsync($"{resource.Route}/{id}");
        if (parent[Doc.Data]![Doc.Relationships] is not JsonObject relationships)
        {
            return; // No relationships advertised (e.g. users) — nothing the spec obliges here.
        }

        foreach (var (name, relationship) in relationships)
        {
            if (relationship![Doc.Links]!.AsObject().TryGetPropertyValue(Doc.Self, out var self) is false)
            {
                continue; // Related-only relationships advertise no linkage URL.
            }

            // Act
            var response = await Client.GetAsync(self!.GetValue<string>());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var linkage = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.True(JsonNode.DeepEquals(relationship[Doc.Data], linkage[Doc.Data]),
                $"'{name}' relationship endpoint linkage {linkage[Doc.Data]?.ToJsonString()} does " +
                $"not match the parent resource's linkage {relationship[Doc.Data]?.ToJsonString()}.");
        }
    }

    /// <summary>"A server MUST respond to a successful request to fetch a relationship with a 200
    /// OK response." — the empty to-one case: linkage is an explicit data: null, not a 404 and not
    /// an omitted member. Deal-specific: deal→contact is this API's nullable to-one.</summary>
    [Fact]
    public async Task FetchingRelationships_EmptyToOne_Returns200WithNullLinkage()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var response = await Client.GetAsync($"{Routes.Deals}/{deal.Id}/relationships/contact");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(document.TryGetPropertyValue(Doc.Data, out var data));
        Assert.Null(data);
    }

    /// <summary>"A server MUST return 404 Not Found when processing a request to fetch a
    /// relationship link URL that does not exist."</summary>
    [Fact]
    public async Task FetchingRelationships_UnknownParentResource_Returns404()
    {
        // Act + Assert
        await Client.GetProblemAsync($"{Routes.Deals}/99999/relationships/owner", 404);
    }

    // ── Inclusion of Related Resources (§ fetching-includes) ────────────────────────────────────

    /// <summary>"If an endpoint supports the include parameter and a client supplies it: The
    /// server's response MUST be a compound document with an included key" and "The server MUST
    /// NOT include unrequested resource objects in the included section of the compound
    /// document."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.IncludableKeys), MemberType = typeof(SpecResources))]
    public async Task Inclusion_SupportedIncludePath_SideloadsOnlyTheRequestedType(string key)
    {
        // Arrange
        var resource = SpecResources.Get(key);
        var include = resource.Include!.Value;
        await ArrangeAsync(resource.ArrangeSeveral);

        // Act
        var document = await Client.GetDocumentAsync($"{resource.Route}?include={include.Path}");

        // Assert — included exists, is non-empty, and holds only the requested type.
        var included = Assert.IsType<JsonArray>(document[Doc.Included]);
        Assert.NotEmpty(included);
        Assert.All(included, item => Assert.Equal(include.Type, item![Doc.Type]!.GetValue<string>()));
    }

    /// <summary>"If a server is unable to identify a relationship path or does not support
    /// inclusion of resources from a path, it MUST respond with 400 Bad Request." and "If an
    /// endpoint does not support the include parameter, it MUST respond with 400 Bad Request to
    /// any requests that include it." — every endpoint, whether it supports includes or not.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task Inclusion_UnknownRelationshipPath_Returns400(string key)
    {
        // Act + Assert
        await Client.GetProblemAsync($"{SpecResources.Get(key).Route}?include=garbage", 400);
    }

    // ── Sparse Fieldsets (§ fetching-sparse-fieldsets) ──────────────────────────────────────────

    /// <summary>"If a client requests a restricted set of fields for a given resource type, an
    /// endpoint MUST NOT include additional fields in resource objects of that type in its
    /// response." — fields cover attributes and relationships alike.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task SparseFieldsets_RestrictedFields_ExcludeEveryOtherField(string key)
    {
        // Arrange
        var resource = SpecResources.Get(key);
        await ArrangeAsync(resource.ArrangeSeveral);

        // Act
        var document = await Client.GetDocumentAsync(
            $"{resource.Route}?fields[{resource.Type}]={resource.SparseField}");

        // Assert — every returned resource carries exactly the requested field and no relationships.
        var data = document[Doc.Data]!.AsArray();
        Assert.NotEmpty(data);
        Assert.All(data, item =>
        {
            var resourceObject = item!.AsObject();
            Assert.Equal([resource.SparseField],
                resourceObject[Doc.Attributes]!.AsObject().Select(property => property.Key));
            Assert.False(resourceObject.ContainsKey(Doc.Relationships));
        });
    }

    /// <summary>"The value of any fields[TYPE] parameter MUST be a comma-separated list that
    /// refers to the name(s) of the fields to be returned. An empty value indicates that no
    /// fields should be returned."</summary>
    [Fact]
    public async Task SparseFieldsets_EmptyFieldset_ReturnsNoFields()
    {
        // Arrange
        var deal = await ArrangeAsync(DealWithoutContact);

        // Act
        var document = await Client.GetDocumentAsync(
            $"{Routes.Deals}/{deal.Id}?fields[{ResourceTypes.Deals}]=");

        // Assert — the resource keeps its identity but carries no fields at all.
        var resource = document[Doc.Data]!.AsObject();
        Assert.False(resource.ContainsKey(Doc.Attributes));
        Assert.False(resource.ContainsKey(Doc.Relationships));
    }

    // ── Sorting (§ fetching-sorting) ────────────────────────────────────────────────────────────

    /// <summary>"If sorting is supported by the server and requested by the client via query
    /// parameter sort, the server MUST return elements of the top-level data array of the response
    /// ordered according to the criteria specified." and "The sort order for each sort field MUST
    /// be ascending unless it is prefixed with a minus (U+002D HYPHEN-MINUS, '-'), in which case
    /// it MUST be descending." — self-validating: the returned attribute values must be ordered,
    /// numerically for numbers and ordinally for strings.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task Sorting_SortParameter_OrdersAscendingByDefaultAndDescendingWithMinus(string key)
    {
        // Arrange — three rows with distinct, out-of-order sort-field values.
        var resource = SpecResources.Get(key);
        await ArrangeAsync(resource.ArrangeSeveral);

        // Act
        var ascending = await Client.GetDocumentAsync($"{resource.Route}?sort={resource.SortField}");
        var descending = await Client.GetDocumentAsync($"{resource.Route}?sort=-{resource.SortField}");

        // Assert
        AssertSorted(ascending, resource.SortField, descending: false);
        AssertSorted(descending, resource.SortField, descending: true);
    }

    /// <summary>"If the server does not support sorting as specified in the query parameter sort,
    /// it MUST return 400 Bad Request."</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task Sorting_UnsupportedSortField_Returns400(string key)
    {
        // Act + Assert
        await Client.GetProblemAsync($"{SpecResources.Get(key).Route}?sort=garbage", 400);
    }

    // ── Pagination (§ fetching-pagination) ──────────────────────────────────────────────────────

    /// <summary>"The following keys MUST be used for pagination links: first, last, prev, next."
    /// and "Keys MUST either be omitted or have a null value to indicate that a particular link is
    /// unavailable." — checked across the first, a middle, and the last page.</summary>
    [Theory]
    [MemberData(nameof(SpecResources.Keys), MemberType = typeof(SpecResources))]
    public async Task Pagination_Links_UseSpecKeysAndOmitOrNullUnavailableOnes(string key)
    {
        // Arrange — three rows at page size 1 give a first, middle, and last page.
        var resource = SpecResources.Get(key);
        await ArrangeAsync(resource.ArrangeSeveral);

        // Act
        var first = await Client.GetDocumentAsync($"{resource.Route}?page[size]=1&page[number]=1");
        var middle = await Client.GetDocumentAsync($"{resource.Route}?page[size]=1&page[number]=2");
        var last = await Client.GetDocumentAsync($"{resource.Route}?page[size]=1&page[number]=3");

        // Assert — the middle page can reach everywhere, the edges lack exactly one neighbour.
        AssertLinkAvailable(middle, Doc.First);
        AssertLinkAvailable(middle, Doc.Prev);
        AssertLinkAvailable(middle, Doc.Next);
        AssertLinkAvailable(middle, Doc.Last);
        AssertLinkUnavailable(first, Doc.Prev);
        AssertLinkAvailable(first, Doc.Next);
        AssertLinkUnavailable(last, Doc.Next);
        AssertLinkAvailable(last, Doc.Prev);
    }

    /// <summary>"Concepts of order, as expressed in the naming of pagination links, MUST remain
    /// consistent with JSON:API's sorting rules." — with a descending amount sort, page 1 holds
    /// the biggest amount and page 2 the next one.</summary>
    [Fact]
    public async Task Pagination_WithSort_StaysConsistentWithSortOrder()
    {
        // Arrange
        await ArrangeAsync(SpecResources.Get("deals").ArrangeSeveral);

        // Act
        var page1 = await Client.GetDocumentAsync($"{Routes.Deals}?sort=-{Attr.Amount}&page[size]=1&page[number]=1");
        var page2 = await Client.GetDocumentAsync($"{Routes.Deals}?sort=-{Attr.Amount}&page[size]=1&page[number]=2");

        // Assert
        Assert.Equal(48000m, Amounts(page1).Single());
        Assert.Equal(12500m, Amounts(page2).Single());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static Deal DealWithoutContact(AppDbContext db) =>
        db.Deals.Add(Rows.Deal("Hardware upgrade", Rows.Company(), Rows.User())).Entity;

    /// <summary>Ids are database-generated, so they are read off the arranged entity after
    /// SaveChanges; every domain entity exposes an int Id.</summary>
    private static int IdOf(object entity) =>
        (int)entity.GetType().GetProperty("Id")!.GetValue(entity)!;

    private static decimal[] Amounts(JsonNode document) =>
        document[Doc.Data]!.AsArray()
            .Select(resource => resource![Doc.Attributes]![Attr.Amount]!.GetValue<decimal>())
            .ToArray();

    /// <summary>Asserts data is ordered by <paramref name="field"/> — numerically when the
    /// attribute is a JSON number, ordinally when it is a string.</summary>
    private static void AssertSorted(JsonNode document, string field, bool descending)
    {
        var values = document[Doc.Data]!.AsArray()
            .Select(resource => resource![Doc.Attributes]![field]!.AsValue())
            .Select(value => value.GetValueKind() == JsonValueKind.Number
                ? (IComparable)value.GetValue<decimal>()
                : value.GetValue<string>())
            .ToList();
        Assert.True(values.Count >= 2, "Sorting needs at least two rows to prove an order.");

        var expected = descending
            ? values.OrderByDescending(value => value, Comparer<IComparable>.Create(CompareOrdinal))
            : values.OrderBy(value => value, Comparer<IComparable>.Create(CompareOrdinal));
        Assert.Equal(expected, values);
    }

    /// <summary>Strings compare ordinally (matching the database's byte-wise collation for the
    /// ASCII test data); other comparables use their natural order.</summary>
    private static int CompareOrdinal(IComparable left, IComparable right)
    {
        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }
        return left.CompareTo(right);
    }

    private static void AssertLinkAvailable(JsonNode document, string key)
    {
        var links = document[Doc.Links]!.AsObject();
        Assert.True(links.TryGetPropertyValue(key, out var value) && value is not null,
            $"links.{key} should be available on this page.");
    }

    /// <summary>The spec allows either omission or an explicit null for an unavailable link.</summary>
    private static void AssertLinkUnavailable(JsonNode document, string key)
    {
        var links = document[Doc.Links]!.AsObject();
        if (links.TryGetPropertyValue(key, out var value))
        {
            Assert.Null(value);
        }
    }
}
