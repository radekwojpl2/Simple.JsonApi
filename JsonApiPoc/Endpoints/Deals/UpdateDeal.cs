using System.Text.Json;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class UpdateDeal
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapPatch("/{id:int}", async (ISender sender, int id, UpdateDealRequest request) =>
        {
            if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
            {
                return JsonApiResults.Validation("The 'title' field cannot be empty.");
            }

            var result = await sender.Send(new UpdateDealCommand(
                id,
                request.Title,
                request.Amount,
                request.Stage,
                request.CloseDate,
                request.CompanyId,
                request.ContactId,
                request.OwnerId,
                request.CustomFields));

            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Partially update a deal from a flat JSON object; only provided fields change, customFields are merged. Returns 204 with no body.")
        .Accepts<UpdateDealRequest>("application/json")
        .Produces(204)
        .ProducesProblem(404)
        .ProducesProblem(422);
}

// Request body for PATCH /api/deals/{id} — all fields optional, omitted fields keep their current value:
// { "stage": "won", "amount": 120000, "customFields": { "probability": 100 } }
public record UpdateDealRequest(string? Title, decimal? Amount, string? Stage, DateTime? CloseDate,
    int? CompanyId, int? ContactId, int? OwnerId, Dictionary<string, JsonElement>? CustomFields);
