using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

// Request body for POST /api/contacts — a JSON:API resource document (application/vnd.api+json is
// the only accepted content type): firstName, lastName, email, phone and customFields as
// attributes; company as a to-one relationship.
public static class CreateContact
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapPost("/", async (ISender sender, ResourceMapRegistry maps, JsonNode? body) =>
        {
            if (ContactDocuments.TryReadCreateCommand(body, out var command) is { } invalid)
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
                Data = maps.Get<Contact>().Build(created.Contact, state: created.CustomFields)
            };
            return JsonApiResults.Created($"/api/contacts/{created.Contact.Id}", document);
        })
        .WithSummary("Create a contact from a JSON:API resource document; the customFields attribute is validated against the contact field definitions. Returns 201 with a Location header and the created resource as a JSON:API document.")
        .WithResourceDocumentBody<ContactDocuments.ContactAttributes>("contacts", update: false,
            new ResourceDocumentRelationshipMetadata("company", "companies", Required: true, Clearable: false))
        .Produces<JsonApiDocument>(201, contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(403)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
}
