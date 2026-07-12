using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealOwner
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapGet("/{id:int}/owner", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: false, IncludeContact: false, IncludeOwner: true));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = maps.Get<User>().Build(result.Owner!, query),
                Links = new JsonApiLinks(Self: $"/api/deals/{id}/owner")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["users"], paging: false)
        .WithSummary("Get a deal's owner (related resource). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
