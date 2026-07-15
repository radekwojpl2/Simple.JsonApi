using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

// Request body for PATCH /api/contacts/{id} — a JSON:API resource document whose type and id
// match the URL. Omitted attributes and relationships keep their current values and customFields
// merge; the company relationship may be repointed but never cleared.
public static class UpdateContact
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapPatch("/{id:int}", async (ISender sender, int id, JsonNode? body) =>
        {
            if (ContactDocuments.TryReadUpdateCommand(body, id, out var command) is { } invalid)
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
        .WithSummary("Partially update a contact from a JSON:API resource document; only provided members change, customFields are merged. Returns 204 with no body.")
        .WithResourceDocumentBody<ContactDocuments.ContactAttributes>("contacts", update: true,
            new ResourceDocumentRelationshipMetadata("company", "companies", Required: false, Clearable: false))
        .Produces(204)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
}
