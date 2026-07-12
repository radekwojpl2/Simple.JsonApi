using JsonApiKit;
using JsonApiPoc.Application.Companies;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Companies;

public static class ListCompanies
{
    public static void Map(RouteGroupBuilder companies) =>
        companies.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetCompaniesQuery(query.Page.Number, query.Page.Size, query.Sort));
            var companyMap = maps.Get<Company>();
            var document = new JsonApiDocument
            {
                Data = result.Companies.Select(c => companyMap.Build(c, query)).ToList(),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(sorts: ["name", "industry"], fieldsFor: ["companies"])
        .WithSummary("List companies. Supports sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);
}
