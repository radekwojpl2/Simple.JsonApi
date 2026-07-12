using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class ListDeals
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetDealsQuery(
                query.Filter("stage"),
                query.Has("company"), query.Has("contact"), query.Has("owner"),
                query.Page.Number, query.Page.Size, query.Sort));

            var dealMap = maps.Get<Deal>();
            var document = new JsonApiDocument
            {
                Data = result.Deals
                    .Select(d => dealMap.Build(d, query, result.DealFields.GetValueOrDefault(d.Id))).ToList(),
                Included = BuildIncluded(result, maps, query),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(
            includes: ["company", "contact", "owner"],
            sorts: ["title", "amount", "stage", "closeDate"],
            filters: new() { ["stage"] = DealStages.All },
            fieldsFor: ["deals", "companies", "contacts", "users"])
        .WithSummary("List deals. Supports include=company,contact,owner, filter[stage], sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);

    private static IReadOnlyList<ResourceObject>? BuildIncluded(DealsResult result, ResourceMapRegistry maps,
        JsonApiQuery query)
    {
        if (result.Companies is null && result.Contacts is null && result.Owners is null)
        {
            return null;
        }

        var included = new List<ResourceObject>();
        if (result.Companies is not null)
        {
            included.AddRange(result.Companies.Select(c => maps.Get<Company>().Build(c, query)));
        }
        if (result.Contacts is not null)
        {
            included.AddRange(result.Contacts.Select(c =>
                maps.Get<Contact>().Build(c, query, result.ContactFields?.GetValueOrDefault(c.Id))));
        }
        if (result.Owners is not null)
        {
            included.AddRange(result.Owners.Select(u => maps.Get<User>().Build(u, query)));
        }

        return included;
    }
}
