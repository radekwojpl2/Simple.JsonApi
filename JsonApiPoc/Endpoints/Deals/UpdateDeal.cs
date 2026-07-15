using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

// Request body for PATCH /api/deals/{id} — a JSON:API resource document whose type and id match
// the URL. Omitted attributes and relationships keep their current values, customFields merge,
// and "contact": { "data": null } clears the contact.
public static class UpdateDeal
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapPatch("/{id:int}", async (ISender sender, int id, JsonNode? body) =>
        {
            if (DealDocuments.TryReadUpdateCommand(body, id, out var command) is { } invalid)
            {
                return invalid;
            }

            var result = await sender.Send(command!);
            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Partially update a deal from a JSON:API resource document; only provided members change, customFields are merged. Returns 204 with no body.")
        .WithResourceDocumentBody<DealDocuments.DealAttributes>("deals", update: true,
            new("company", "companies", Required: false, Clearable: false),
            new("contact", "contacts", Required: false, Clearable: true),
            new("owner", "users", Required: false, Clearable: false))
        .Produces(204)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
}
