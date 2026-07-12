using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealContactRelationship
{
    public static void Map(RouteGroupBuilder deals)
    {
        deals.MapGet("/{id:int}/relationships/contact", async (ISender sender, int id) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: false, IncludeContact: false, IncludeOwner: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            ResourceIdentifier? data = null;
            if (result.Deal.ContactId is { } contactId)
            {
                data = new ResourceIdentifier("contacts", contactId.ToString());
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = data,
                Links = new JsonApiLinks(
                    Self: $"/api/deals/{id}/relationships/contact",
                    Related: $"/api/deals/{id}/contact")
            });
        })
        .WithSummary("Get the deal→contact relationship linkage (resource identifier, or null data when unset).")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);

        // Spec to-one relationship update (https://jsonapi.org/format/#crud-updating-to-one-relationships):
        // data is a resource identifier to repoint the contact, or null to clear it.
        deals.MapPatch("/{id:int}/relationships/contact", async (ISender sender, int id, JsonNode? body) =>
        {
            if (ToOneLinkage.TryParse(body, "contacts", out var targetId) is { } invalid)
            {
                return invalid;
            }

            int? contactId = null;
            if (targetId is not null)
            {
                if (!int.TryParse(targetId, out var parsed))
                {
                    return JsonApiResults.NotFound($"Contact '{targetId}' does not exist.");
                }
                contactId = parsed;
            }

            var result = await sender.Send(new SetDealContactCommand(id, contactId));
            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Replace the deal→contact relationship from a to-one linkage document: {\"data\":{\"type\":\"contacts\",\"id\":\"5\"}}, or {\"data\":null} to clear. Returns 204.")
        .WithToOneLinkageBody("contacts", clearable: true)
        .Produces(204)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409);
    }
}
