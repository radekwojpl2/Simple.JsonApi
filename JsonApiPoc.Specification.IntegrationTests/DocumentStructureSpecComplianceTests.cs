using System.Text.Json.Nodes;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>Checks the JSON:API 1.1 "Document Structure" section
/// (https://jsonapi.org/format/#document-structure) rules that the per-endpoint golden tests do
/// not already pin. Each test quotes verbatim the sentence it enforces.</summary>
[Collection(ApiCollection.Name)]
public class DocumentStructureSpecComplianceTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>"The members data and errors MUST NOT coexist in the same document." — every
    /// successful document carries data and must not carry errors.</summary>
    [Fact]
    public async Task TopLevel_DataAndErrors_NeverCoexist()
    {
        // Arrange
        var deal = await ArrangeAsync(OneDeal);

        // Act
        var collection = (await Client.GetDocumentAsync(Routes.Deals)).AsObject();
        var single = (await Client.GetDocumentAsync($"{Routes.Deals}/{deal.Id}")).AsObject();

        // Assert
        Assert.True(collection.ContainsKey(Doc.Data));
        Assert.False(collection.ContainsKey(Doc.Errors));
        Assert.True(single.ContainsKey(Doc.Data));
        Assert.False(single.ContainsKey(Doc.Errors));
    }

    /// <summary>"A compound document MUST NOT include more than one resource object for each type
    /// and id pair." — two deals sharing one company must sideload that company once, not once
    /// per referencing deal.</summary>
    [Fact]
    public async Task CompoundDocument_SharedRelatedResource_IsIncludedOnlyOnce()
    {
        // Arrange — two deals pointing at the same company.
        var (first, second) = await ArrangeAsync(db =>
        {
            var company = Rows.Company();
            var owner = Rows.User();
            return (db.Deals.Add(Rows.Deal("First deal", company, owner)).Entity,
                db.Deals.Add(Rows.Deal("Second deal", company, owner)).Entity);
        });

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Deals}?include={Rel.Company}");

        // Assert — both deals came back, but the shared company appears exactly once.
        Assert.Equal(2, document[Doc.Data]!.AsArray().Count);
        var included = Assert.IsType<JsonArray>(document[Doc.Included]);
        var company = Assert.Single(included);
        Assert.Equal(ResourceTypes.Companies, company![Doc.Type]!.GetValue<string>());
        Assert.Equal(first.CompanyId.ToString(), company[Doc.Id]!.GetValue<string>());
        Assert.Equal(second.CompanyId.ToString(), company[Doc.Id]!.GetValue<string>());
    }

    /// <summary>"Every included resource object MUST be identified via a chain of relationships
    /// originating in a document's primary data." (full linkage) — each included resource is
    /// referenced by some primary resource's linkage data.</summary>
    [Fact]
    public async Task CompoundDocument_IncludedResources_AreLinkedFromPrimaryData()
    {
        // Arrange
        await ArrangeAsync(OneDeal);

        // Act
        var document = await Client.GetDocumentAsync(
            $"{Routes.Deals}?include={Rel.Company},{Rel.Owner}");

        // Assert — collect every (type, id) the primary data links to, then require each included
        // resource to be among them.
        var linked = new HashSet<(string Type, string Id)>();
        foreach (var resource in document[Doc.Data]!.AsArray())
        {
            foreach (var (_, relationship) in resource![Doc.Relationships]!.AsObject())
            {
                if (relationship![Doc.Data] is JsonObject identifier)
                {
                    linked.Add((identifier[Doc.Type]!.GetValue<string>(),
                        identifier[Doc.Id]!.GetValue<string>()));
                }
            }
        }
        foreach (var included in document[Doc.Included]!.AsArray())
        {
            var identity = (included![Doc.Type]!.GetValue<string>(),
                included[Doc.Id]!.GetValue<string>());
            Assert.Contains(identity, linked);
        }
    }

    private static Deal OneDeal(AppDbContext db) =>
        db.Deals.Add(Rows.Deal("ERP integration", Rows.Company(), Rows.User())).Entity;
}
