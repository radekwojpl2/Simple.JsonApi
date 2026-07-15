using System.Text.Json;
using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Contacts;

namespace JsonApiPoc.Endpoints.Contacts;

/// <summary>Maps JSON:API resource documents — the API's only write contract — onto the contact
/// write commands: attributes carry the contact's own fields, the company relationship carries
/// its linkage.</summary>
internal static class ContactDocuments
{
    private static readonly string[] Relationships = ["company"];

    /// <summary>The contact attributes a resource document may carry; the company target arrives
    /// under 'relationships', never as an attribute. Internal because the endpoints reference it
    /// as the OpenAPI attributes schema via WithResourceDocumentBody.</summary>
    internal sealed record ContactAttributes(string? FirstName, string? LastName, string? Email,
        string? Phone, Dictionary<string, JsonElement>? CustomFields);

    internal static IResult? TryReadCreateCommand(JsonNode? body, out CreateContactCommand? command)
    {
        command = null;
        if (ResourceDocument.TryParseCreate(body, "contacts", out var document) is { } invalid)
        {
            return invalid;
        }
        if (document.TryReadAttributes<ContactAttributes>(out var attributes) is { } malformed)
        {
            return malformed;
        }
        if (string.IsNullOrWhiteSpace(attributes!.FirstName) || string.IsNullOrWhiteSpace(attributes.LastName))
        {
            return JsonApiResults.Validation("The 'firstName' and 'lastName' fields are required.");
        }
        if (document.RejectUnknownRelationships(Relationships) is { } unknown)
        {
            return unknown;
        }
        if (TryReadCompany(document, required: true, out var companyId) is { } badCompany)
        {
            return badCompany;
        }

        command = new CreateContactCommand(attributes.FirstName, attributes.LastName,
            attributes.Email ?? "", attributes.Phone ?? "", companyId!.Value, attributes.CustomFields);
        return null;
    }

    internal static IResult? TryReadUpdateCommand(JsonNode? body, int id, out UpdateContactCommand? command)
    {
        command = null;
        if (ResourceDocument.TryParseUpdate(body, "contacts", id.ToString(), out var document) is { } invalid)
        {
            return invalid;
        }
        if (document.TryReadAttributes<ContactAttributes>(out var attributes) is { } malformed)
        {
            return malformed;
        }
        if (attributes!.FirstName is not null && string.IsNullOrWhiteSpace(attributes.FirstName)
            || attributes.LastName is not null && string.IsNullOrWhiteSpace(attributes.LastName))
        {
            return JsonApiResults.Validation("The 'firstName' and 'lastName' fields cannot be empty.");
        }
        if (document.RejectUnknownRelationships(Relationships) is { } unknown)
        {
            return unknown;
        }
        if (TryReadCompany(document, required: false, out var companyId) is { } badCompany)
        {
            return badCompany;
        }

        command = new UpdateContactCommand(id, attributes.FirstName, attributes.LastName,
            attributes.Email, attributes.Phone, companyId, attributes.CustomFields);
        return null;
    }

    /// <summary>A contact always belongs to a company, so linkage may repoint the relationship but
    /// never clear it; <paramref name="required"/> additionally demands it on create. A non-numeric
    /// id cannot reference an existing row, so it is the spec's 404 for a related resource that
    /// does not exist.</summary>
    private static IResult? TryReadCompany(ResourceDocument document, bool required, out int? id)
    {
        id = null;
        if (document.TryGetToOne("company", "companies", out var present, out var linkage) is { } invalid)
        {
            return invalid;
        }
        if (!present)
        {
            if (required)
            {
                return JsonApiResults.Validation("The 'company' relationship is required.");
            }
            return null;
        }
        if (linkage is null)
        {
            return JsonApiResults.Validation("The 'company' relationship cannot be cleared.");
        }
        if (!int.TryParse(linkage, out var parsed))
        {
            return JsonApiResults.NotFound($"Company '{linkage}' does not exist.");
        }
        id = parsed;
        return null;
    }
}
