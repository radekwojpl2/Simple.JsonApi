using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class DeleteContact
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapDelete("/{id:int}", async (ISender sender, int id) =>
        {
            var deleted = await sender.Send(new DeleteContactCommand(id));
            if (!deleted)
            {
                return JsonApiResults.NotFound($"Contact '{id}' does not exist.");
            }

            return Results.NoContent();
        })
        .WithSummary("Delete a contact; referencing deals and activities keep working with contact set to null.")
        .Produces(204)
        .ProducesProblem(404);
}
