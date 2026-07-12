using System.Text.Json;
using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class CreateContact
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapPost("/", async (ISender sender, ResourceMapRegistry maps,
            CreateContactRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return JsonApiResults.Validation("The 'firstName' and 'lastName' fields are required.");
            }

            if (request.CompanyId is not { } companyId)
            {
                return JsonApiResults.Validation("The 'companyId' field is required.");
            }

            var result = await sender.Send(new CreateContactCommand(
                request.FirstName,
                request.LastName,
                request.Email ?? "",
                request.Phone ?? "",
                companyId,
                request.CustomFields));

            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            var created = result.Value!;
            var document = new JsonApiDocument
            {
                Data = maps.Get<Contact>().Build(created.Contact, state: created.CustomFields)
            };
            return JsonApiResults.Created($"/api/contacts/{created.Contact.Id}", document);
        })
        .WithSummary("Create a contact from a flat JSON object; customFields is validated against the contact field definitions. Returns 201 with a Location header and the created resource as a JSON:API document.")
        .Accepts<CreateContactRequest>("application/json")
        .Produces<JsonApiDocument>(201, contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(422);
}

// Request body for POST /api/contacts:
// { "firstName": "...", "lastName": "...", "email": "...", "phone": "...", "companyId": 1, "customFields": { "leadSource": "referral" } }
public record CreateContactRequest(string? FirstName, string? LastName, string? Email, string? Phone,
    int? CompanyId, Dictionary<string, JsonElement>? CustomFields);
