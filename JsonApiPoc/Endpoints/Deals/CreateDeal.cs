using System.Text.Json;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class CreateDeal
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapPost("/", async (ISender sender, ResourceMapRegistry maps,
            CreateDealRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return JsonApiResults.Validation("The 'title' field is required.");
            }

            if (request.CompanyId is not { } companyId)
            {
                return JsonApiResults.Validation("The 'companyId' field is required.");
            }

            if (request.OwnerId is not { } ownerId)
            {
                return JsonApiResults.Validation("The 'ownerId' field is required.");
            }

            var result = await sender.Send(new CreateDealCommand(
                request.Title,
                request.Amount ?? 0m,
                request.Stage ?? DealStages.Lead,
                request.CloseDate,
                companyId,
                request.ContactId,
                ownerId,
                request.CustomFields));

            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            var created = result.Value!;
            var document = new JsonApiDocument
            {
                Data = maps.Get<Deal>().Build(created.Deal, state: created.CustomFields)
            };
            return JsonApiResults.Created($"/api/deals/{created.Deal.Id}", document);
        })
        .WithSummary("Create a deal from a flat JSON object; customFields is validated against the deal field definitions. Returns 201 with a Location header and the created resource as a JSON:API document.")
        .Accepts<CreateDealRequest>("application/json")
        .Produces<JsonApiDocument>(201, contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(422);
}

// Request body for POST /api/deals:
// { "title": "...", "amount": 1000, "stage": "lead", "companyId": 1, "contactId": 2, "ownerId": 1, "customFields": { "probability": 40 } }
public record CreateDealRequest(string? Title, decimal? Amount, string? Stage, DateTime? CloseDate,
    int? CompanyId, int? ContactId, int? OwnerId, Dictionary<string, JsonElement>? CustomFields);
