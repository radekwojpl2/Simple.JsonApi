using System.Text.Json;
using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class UpdateContact
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapPatch("/{id:int}", async (ISender sender, int id, UpdateContactRequest request) =>
        {
            if (request.FirstName is not null && string.IsNullOrWhiteSpace(request.FirstName)
                || request.LastName is not null && string.IsNullOrWhiteSpace(request.LastName))
            {
                return JsonApiResults.Validation("The 'firstName' and 'lastName' fields cannot be empty.");
            }

            var result = await sender.Send(new UpdateContactCommand(
                id,
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.CompanyId,
                request.CustomFields));

            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Partially update a contact from a flat JSON object; only provided fields change, customFields are merged. Returns 204 with no body.")
        .Accepts<UpdateContactRequest>("application/json")
        .Produces(204)
        .ProducesProblem(404)
        .ProducesProblem(422);
}

// Request body for PATCH /api/contacts/{id} — all fields optional, omitted fields keep their current value:
// { "email": "new@example.com", "customFields": { "newsletterOptIn": true } }
public record UpdateContactRequest(string? FirstName, string? LastName, string? Email, string? Phone,
    int? CompanyId, Dictionary<string, JsonElement>? CustomFields);
