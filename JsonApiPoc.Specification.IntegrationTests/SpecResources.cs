using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>One entry per resource the API serves. The spec-compliance theories run against every
/// entry, so a new endpoint gets the whole fetching suite by adding one entry here — no new tests.
/// </summary>
/// <param name="Route">Collection base path.</param>
/// <param name="Type">JSON:API resource type string.</param>
/// <param name="HasGetById">Whether GET {Route}/{id} exists; by-id theories skip entries without it.</param>
/// <param name="SortField">A sortable attribute; <paramref name="ArrangeSeveral"/> must plant
/// distinct, out-of-order values for it so an unsorted response cannot pass.</param>
/// <param name="SparseField">An attribute always present on arranged rows, for fieldset theories.</param>
/// <param name="Include">A supported include path and the resource type it sideloads, or null when
/// the list endpoint supports no includes.</param>
/// <param name="ArrangeOne">Plants one row with every relationship populated and returns the
/// entity (its Id is read after SaveChanges).</param>
/// <param name="ArrangeSeveral">Plants exactly three rows with distinct SortField values.</param>
public sealed record SpecResource(
    string Route,
    string Type,
    bool HasGetById,
    string SortField,
    string SparseField,
    (string Path, string Type)? Include,
    Func<AppDbContext, object> ArrangeOne,
    Action<AppDbContext> ArrangeSeveral);

public static class SpecResources
{
    public static readonly IReadOnlyDictionary<string, SpecResource> All = new Dictionary<string, SpecResource>
    {
        ["companies"] = new(
            Routes.Companies, ResourceTypes.Companies, HasGetById: true,
            SortField: Attr.Name, SparseField: Attr.Name, Include: null,
            ArrangeOne: db => db.Companies.Add(Rows.Company()).Entity,
            ArrangeSeveral: db => db.Companies.AddRange(
                Rows.Company("Meridian Logistics", "Logistics"),
                Rows.Company("Acme Manufacturing", "Manufacturing"),
                Rows.Company("Zephyr Labs", "Software"))),

        ["contacts"] = new(
            Routes.Contacts, ResourceTypes.Contacts, HasGetById: true,
            SortField: Attr.LastName, SparseField: Attr.FirstName,
            Include: ("company", ResourceTypes.Companies),
            ArrangeOne: db => db.Contacts.Add(Rows.Contact("Jan", "Kowalski", Rows.Company())).Entity,
            ArrangeSeveral: db =>
            {
                var company = Rows.Company();
                db.Contacts.AddRange(
                    Rows.Contact("Piotr", "Nowak", company),
                    Rows.Contact("Anna", "Adamska", company),
                    Rows.Contact("Ewa", "Zielinska", company));
            }),

        ["users"] = new(
            Routes.Users, ResourceTypes.Users, HasGetById: true,
            SortField: Attr.Name, SparseField: Attr.Name, Include: null,
            ArrangeOne: db => db.Users.Add(Rows.User()).Entity,
            ArrangeSeveral: db => db.Users.AddRange(
                Rows.User("Sarah Chen"), Rows.User("Adam Bell"), Rows.User("Zoe Ward"))),

        ["deals"] = new(
            Routes.Deals, ResourceTypes.Deals, HasGetById: true,
            SortField: Attr.Amount, SparseField: Attr.Title,
            Include: ("company", ResourceTypes.Companies),
            ArrangeOne: db =>
            {
                var company = Rows.Company();
                return db.Deals.Add(Rows.Deal("ERP integration", company, Rows.User(),
                    contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
            },
            // Amounts chosen so numeric and lexical ordering disagree in both directions.
            ArrangeSeveral: db =>
            {
                var company = Rows.Company();
                var owner = Rows.User();
                db.Deals.AddRange(
                    Rows.Deal("Middle", company, owner, amount: 12500m),
                    Rows.Deal("Smallest", company, owner, amount: 8000m),
                    Rows.Deal("Biggest", company, owner, amount: 48000m));
            }),

        ["activities"] = new(
            Routes.Activities, ResourceTypes.Activities, HasGetById: true,
            SortField: Attr.Kind, SparseField: Attr.Subject,
            Include: ("deal", ResourceTypes.Deals),
            ArrangeOne: db =>
            {
                var company = Rows.Company();
                return db.Activities.Add(Rows.Activity("Discovery call", kind: "call",
                    deal: Rows.Deal("ERP integration", company, Rows.User()),
                    contact: Rows.Contact("Jan", "Kowalski", company))).Entity;
            },
            ArrangeSeveral: db =>
            {
                var deal = Rows.Deal("ERP integration", Rows.Company(), Rows.User());
                db.Activities.AddRange(
                    Rows.Activity("Kickoff", kind: "task", deal: deal),
                    Rows.Activity("Intro", kind: "call", deal: deal),
                    Rows.Activity("Follow-up", kind: "email", deal: deal));
            }),

        ["custom-fields"] = new(
            Routes.CustomFields, ResourceTypes.CustomFields, HasGetById: false,
            SortField: Attr.Key, SparseField: Attr.Key, Include: null,
            ArrangeOne: db => db.CustomFieldDefinitions.Add(Rows.Field(ResourceTypes.Contacts, "leadSource")).Entity,
            ArrangeSeveral: db => db.CustomFieldDefinitions.AddRange(
                Rows.Field(ResourceTypes.Contacts, "kilo"),
                Rows.Field(ResourceTypes.Deals, "alpha", dataType: "number"),
                Rows.Field(ResourceTypes.Companies, "zulu")))
    };

    public static SpecResource Get(string key) => All[key];

    public static TheoryData<string> Keys() => Data(All.Keys);

    public static TheoryData<string> GetByIdKeys() =>
        Data(All.Where(entry => entry.Value.HasGetById).Select(entry => entry.Key));

    public static TheoryData<string> IncludableKeys() =>
        Data(All.Where(entry => entry.Value.Include is not null).Select(entry => entry.Key));

    private static TheoryData<string> Data(IEnumerable<string> keys)
    {
        var data = new TheoryData<string>();
        foreach (var key in keys)
        {
            data.Add(key);
        }
        return data;
    }
}
