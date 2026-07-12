using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Activities;

public static class ListActivities
{
    public static void Map(RouteGroupBuilder activities) =>
        activities.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetActivitiesQuery(
                query.Has("deal"), query.Has("contact"), query.Page.Number, query.Page.Size, query.Sort));

            var activityMap = maps.Get<Activity>();
            var document = new JsonApiDocument
            {
                Data = result.Activities.Select(a => activityMap.Build(a, query)).ToList(),
                Included = ActivityIncluded.Build(result, maps, query),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(includes: ["deal", "contact"], sorts: ["dueAt", "kind"],
            fieldsFor: ["activities", "deals", "contacts"])
        .WithSummary("List activities. Supports include=deal,contact, sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);
}
