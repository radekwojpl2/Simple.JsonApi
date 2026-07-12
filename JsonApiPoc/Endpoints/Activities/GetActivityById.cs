using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Activities;

public static class GetActivityById
{
    public static void Map(RouteGroupBuilder activities) =>
        activities.MapGet("/{id:int}", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetActivityByIdQuery(id, query.Has("deal"), query.Has("contact")));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Activity '{id}' does not exist.");
            }

            var document = new JsonApiDocument
            {
                Data = maps.Get<Activity>().Build(result.Activities[0], query),
                Included = ActivityIncluded.Build(result, maps, query)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(includes: ["deal", "contact"],
            fieldsFor: ["activities", "deals", "contacts"], paging: false)
        .WithSummary("Get an activity by id. Supports include=deal,contact and fields[type].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404);
}
