using JsonApiKit;
using JsonApiPoc.Application.Deals;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class DeleteDeal
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapDelete("/{id:int}", async (ISender sender, int id) =>
        {
            var deleted = await sender.Send(new DeleteDealCommand(id));
            if (!deleted)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            return Results.NoContent();
        })
        .WithSummary("Delete a deal and its custom field values.")
        .Produces(204)
        .ProducesProblem(404);
}
