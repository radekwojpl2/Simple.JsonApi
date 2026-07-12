using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Activities;

public static class GetActivityDeal
{
    public static void Map(RouteGroupBuilder activities) =>
        activities.MapGet("/{id:int}/deal", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetActivityByIdQuery(id, IncludeDeal: true, IncludeContact: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Activity '{id}' does not exist.");
            }

            // data stays null when the activity has no deal — an empty to-one, not a 404.
            ResourceObject? data = null;
            var deal = result.Deals?.FirstOrDefault();
            if (deal is not null)
            {
                data = maps.Get<Deal>().Build(deal, query, result.DealFields?.GetValueOrDefault(deal.Id));
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = data,
                Links = new JsonApiLinks(Self: $"/api/activities/{id}/deal")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["deals"], paging: false)
        .WithSummary("Get an activity's deal (related resource; data is null when unset). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
