using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Activities;

public static class GetActivityDealRelationship
{
    public static void Map(RouteGroupBuilder activities)
    {
        activities.MapGet("/{id:int}/relationships/deal", async (ISender sender, int id) =>
        {
            var result = await sender.Send(new GetActivityByIdQuery(id, IncludeDeal: false, IncludeContact: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Activity '{id}' does not exist.");
            }

            ResourceIdentifier? data = null;
            if (result.Activities[0].DealId is { } dealId)
            {
                data = new ResourceIdentifier("deals", dealId.ToString());
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = data,
                Links = new JsonApiLinks(
                    Self: $"/api/activities/{id}/relationships/deal",
                    Related: $"/api/activities/{id}/deal")
            });
        })
        .WithSummary("Get the activity→deal relationship linkage (resource identifier, or null data when unset).")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);

        // The spec requires an advertised relationship URL to refuse unsupported updates with 403,
        // not 405 (https://jsonapi.org/format/#crud-updating-relationship-responses-403).
        activities.MapPatch("/{id:int}/relationships/deal", () =>
                JsonApiResults.Forbidden("This API does not support updating relationships."))
            .WithSummary("Updating the activity→deal relationship is not supported.")
            .ProducesProblem(403);
    }
}
