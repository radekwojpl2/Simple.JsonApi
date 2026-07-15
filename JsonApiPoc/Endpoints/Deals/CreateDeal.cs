using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

// Request body for POST /api/deals — a JSON:API resource document (application/vnd.api+json is
// the only accepted content type; anything else draws a 415 from the content negotiation
// middleware): title, amount, stage, closeDate and customFields as attributes; company, contact
// and owner as to-one relationships.
public static class CreateDeal
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapPost("/", async (ISender sender, ResourceMapRegistry maps, JsonNode? body) =>
        {
            if (DealDocuments.TryReadCreateCommand(body, out var command) is { } invalid)
            {
                return invalid;
            }

            var result = await sender.Send(command!);
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
        .WithSummary("Create a deal from a JSON:API resource document; the customFields attribute is validated against the deal field definitions. Returns 201 with a Location header and the created resource as a JSON:API document.")
        .WithResourceDocumentBody<DealDocuments.DealAttributes>("deals", update: false,
            new("company", "companies", Required: true, Clearable: false),
            new("contact", "contacts", Required: false, Clearable: true),
            new("owner", "users", Required: true, Clearable: false))
        .Produces<JsonApiDocument>(201, contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(403)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
}
