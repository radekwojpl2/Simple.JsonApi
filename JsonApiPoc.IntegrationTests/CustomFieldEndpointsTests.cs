using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CustomFieldEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private CustomFieldDefinition _probability = null!;
    private CustomFieldDefinition _leadSource = null!;
    private CustomFieldDefinition _competitor = null!;

    [Fact]
    public async Task List_Default_ReturnsAllDefinitions()
    {
        // Arrange
        await ArrangeAsync(TwoDealFieldsAndOneContactField);

        // Act
        var document = await Client.GetDocumentAsync(Routes.CustomFields);

        // Assert — all three definitions in insertion (id) order, full document.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.CustomFields,
                null,
                1, 25, total: 3,
                [Document.CustomField(_probability), Document.CustomField(_leadSource), Document.CustomField(_competitor)]));
    }

    [Fact]
    public async Task List_FilterByResourceType_ReturnsMatchingDefinitions()
    {
        // Arrange
        await ArrangeAsync(TwoDealFieldsAndOneContactField);

        // Act
        var document = await Client.GetDocumentAsync(
            $"{Routes.CustomFields}?filter[resourceType]={ResourceTypes.Deals}");

        // Assert — only the deal fields survive the filter; the filter rides along in the page links.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.CustomFields,
                $"filter[resourceType]={ResourceTypes.Deals}",
                1, 25, total: 2,
                [Document.CustomField(_probability), Document.CustomField(_competitor)]));
    }

    [Fact]
    public async Task List_SortByResourceTypeDescending_PutsDealFieldsFirst()
    {
        // Arrange
        await ArrangeAsync(TwoDealFieldsAndOneContactField);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.CustomFields}?sort=-resourceType");

        // Assert — "deals" sorts after "contacts", so descending puts both deal fields first.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.CustomFields,
                "sort=-resourceType",
                1, 25, total: 3,
                [Document.CustomField(_probability), Document.CustomField(_competitor), Document.CustomField(_leadSource)]));
    }

    [Fact]
    public async Task List_SortByKey_OrdersAlphabetically()
    {
        // Arrange
        await ArrangeAsync(TwoDealFieldsAndOneContactField);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.CustomFields}?sort={Attr.Key}");

        // Assert — across both resource types: competitor < leadSource < probability.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.CustomFields,
                $"sort={Attr.Key}",
                1, 25, total: 3,
                [Document.CustomField(_competitor), Document.CustomField(_leadSource), Document.CustomField(_probability)]));
    }

    [Fact]
    public async Task List_FilterAndSortTogether_SortsOnlyTheFilteredSet()
    {
        // Arrange
        await ArrangeAsync(TwoDealFieldsAndOneContactField);

        // Act
        var query = $"filter[resourceType]={ResourceTypes.Deals}&sort=-{Attr.Key}";
        var document = await Client.GetDocumentAsync($"{Routes.CustomFields}?{query}");

        // Assert — the deal fields only, keys descending, and meta.total counts the filtered set.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.CustomFields,
                query,
                1, 25, total: 2,
                [Document.CustomField(_probability), Document.CustomField(_competitor)]));
    }

    [Fact]
    public async Task List_UnknownResourceTypeFilter_Returns400()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.CustomFields}?filter[resourceType]=bogus", 400);

        // Assert — the detail lists the valid values in declaration order.
        problem.ShouldMatchExactly(Document.Problem(400, "Invalid filter",
            "Unknown resourceType 'bogus'. Valid values: contacts, deals, companies."));
    }

    /// <summary>Keys interleave across the two resource types (competitor &lt; leadSource &lt;
    /// probability), so a filter that leaked rows shows up in a key-sorted assertion.</summary>
    private void TwoDealFieldsAndOneContactField(AppDbContext db)
    {
        _probability = db.CustomFieldDefinitions.Add(
            Rows.Field(ResourceTypes.Deals, Attr.Probability, dataType: "number")).Entity;
        _leadSource = db.CustomFieldDefinitions.Add(
            Rows.Field(ResourceTypes.Contacts, Attr.LeadSource)).Entity;
        _competitor = db.CustomFieldDefinitions.Add(
            Rows.Field(ResourceTypes.Deals, Attr.Competitor)).Entity;
    }
}
