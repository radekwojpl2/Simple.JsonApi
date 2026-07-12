using JsonApiKit;
using JsonApiPoc.Application.Companies;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Companies;

public static class GetCompanyById
{
    public static void Map(RouteGroupBuilder companies) =>
        companies.MapGet("/{id:int}", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var company = await sender.Send(new GetCompanyByIdQuery(id));
            if (company is null)
            {
                return JsonApiResults.NotFound($"Company '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiDocument { Data = maps.Get<Company>().Build(company, query) });
        })
        .WithJsonApiQuery(fieldsFor: ["companies"], paging: false)
        .WithSummary("Get a company by id. Supports fields[type].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
