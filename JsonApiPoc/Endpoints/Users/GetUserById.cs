using JsonApiKit;
using JsonApiPoc.Application.Users;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Users;

public static class GetUserById
{
    public static void Map(RouteGroupBuilder users) =>
        users.MapGet("/{id:int}", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var user = await sender.Send(new GetUserByIdQuery(id));
            if (user is null)
            {
                return JsonApiResults.NotFound($"User '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiDocument { Data = maps.Get<User>().Build(user, query) });
        })
        .WithJsonApiQuery(fieldsFor: ["users"], paging: false)
        .WithSummary("Get a user by id. Supports fields[type].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
