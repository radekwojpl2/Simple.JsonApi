using JsonApiKit;
using JsonApiPoc.Application.Users;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Users;

public static class ListUsers
{
    public static void Map(RouteGroupBuilder users) =>
        users.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetUsersQuery(query.Page.Number, query.Page.Size, query.Sort));
            var userMap = maps.Get<User>();
            var document = new JsonApiDocument
            {
                Data = result.Users.Select(u => userMap.Build(u, query)).ToList(),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(sorts: ["name", "email"], fieldsFor: ["users"])
        .WithSummary("List users (deal owners). Supports sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);
}
