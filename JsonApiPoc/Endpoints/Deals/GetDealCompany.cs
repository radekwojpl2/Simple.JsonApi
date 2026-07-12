using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealCompany
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapGet("/{id:int}/company", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: true, IncludeContact: false, IncludeOwner: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = maps.Get<Company>().Build(result.Company!, query),
                Links = new JsonApiLinks(Self: $"/api/deals/{id}/company")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["companies"], paging: false)
        .WithSummary("Get a deal's company (related resource). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
