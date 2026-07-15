using System.Text.Json;
using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Endpoints.Deals;

/// <summary>Maps JSON:API resource documents — the API's only write contract — onto the deal
/// write commands: attributes carry the deal's own fields, relationships carry the
/// company/contact/owner linkage.</summary>
internal static class DealDocuments
{
    private static readonly string[] Relationships = ["company", "contact", "owner"];

    /// <summary>The deal attributes a resource document may carry; relationship targets arrive
    /// under 'relationships', never as attributes. Internal because the endpoints reference it as
    /// the OpenAPI attributes schema via WithResourceDocumentBody.</summary>
    internal sealed record DealAttributes(string? Title, decimal? Amount, string? Stage,
        DateTime? CloseDate, Dictionary<string, JsonElement>? CustomFields);

    internal static IResult? TryReadCreateCommand(JsonNode? body, out CreateDealCommand? command)
    {
        command = null;
        if (ResourceDocument.TryParseCreate(body, "deals", out var document) is { } invalid)
        {
            return invalid;
        }
        if (document.TryReadAttributes<DealAttributes>(out var attributes) is { } malformed)
        {
            return malformed;
        }
        if (string.IsNullOrWhiteSpace(attributes!.Title))
        {
            return JsonApiResults.Validation("The 'title' field is required.");
        }
        if (document.RejectUnknownRelationships(Relationships) is { } unknown)
        {
            return unknown;
        }
        if (TryReadRequired(document, "company", "companies", "Company", required: true, out var companyId) is { } badCompany)
        {
            return badCompany;
        }
        if (TryReadRequired(document, "owner", "users", "User", required: true, out var ownerId) is { } badOwner)
        {
            return badOwner;
        }
        if (TryReadContact(document, out _, out var contactId) is { } badContact)
        {
            return badContact;
        }

        command = new CreateDealCommand(attributes.Title, attributes.Amount ?? 0m,
            attributes.Stage ?? DealStages.Lead, attributes.CloseDate,
            companyId!.Value, contactId, ownerId!.Value, attributes.CustomFields);
        return null;
    }

    internal static IResult? TryReadUpdateCommand(JsonNode? body, int id, out UpdateDealCommand? command)
    {
        command = null;
        if (ResourceDocument.TryParseUpdate(body, "deals", id.ToString(), out var document) is { } invalid)
        {
            return invalid;
        }
        if (document.TryReadAttributes<DealAttributes>(out var attributes) is { } malformed)
        {
            return malformed;
        }
        if (attributes!.Title is not null && string.IsNullOrWhiteSpace(attributes.Title))
        {
            return JsonApiResults.Validation("The 'title' field cannot be empty.");
        }
        if (document.RejectUnknownRelationships(Relationships) is { } unknown)
        {
            return unknown;
        }
        if (TryReadRequired(document, "company", "companies", "Company", required: false, out var companyId) is { } badCompany)
        {
            return badCompany;
        }
        if (TryReadRequired(document, "owner", "users", "User", required: false, out var ownerId) is { } badOwner)
        {
            return badOwner;
        }
        if (TryReadContact(document, out var contactPresent, out var contactId) is { } badContact)
        {
            return badContact;
        }

        command = new UpdateDealCommand(id, attributes.Title, attributes.Amount, attributes.Stage,
            attributes.CloseDate, companyId, contactId, ownerId, attributes.CustomFields,
            ClearContact: contactPresent && contactId is null);
        return null;
    }

    /// <summary>Company and owner are required on a deal, so linkage may repoint them but never
    /// clear them; <paramref name="required"/> additionally demands the relationship on create.
    /// A non-numeric id cannot reference an existing row, so it is the spec's 404 for a related
    /// resource that does not exist.</summary>
    private static IResult? TryReadRequired(ResourceDocument document, string name, string targetType,
        string targetLabel, bool required, out int? id)
    {
        id = null;
        if (document.TryGetToOne(name, targetType, out var present, out var linkage) is { } invalid)
        {
            return invalid;
        }
        if (!present)
        {
            if (required)
            {
                return JsonApiResults.Validation($"The '{name}' relationship is required.");
            }
            return null;
        }
        if (linkage is null)
        {
            return JsonApiResults.Validation($"The '{name}' relationship cannot be cleared.");
        }
        if (!int.TryParse(linkage, out var parsed))
        {
            return JsonApiResults.NotFound($"{targetLabel} '{linkage}' does not exist.");
        }
        id = parsed;
        return null;
    }

    /// <summary>The deal→contact relationship is the nullable one: data null clears it
    /// (<paramref name="present"/> true, <paramref name="id"/> null).</summary>
    private static IResult? TryReadContact(ResourceDocument document, out bool present, out int? id)
    {
        id = null;
        if (document.TryGetToOne("contact", "contacts", out present, out var linkage) is { } invalid)
        {
            return invalid;
        }
        if (linkage is null)
        {
            return null;
        }
        if (!int.TryParse(linkage, out var parsed))
        {
            return JsonApiResults.NotFound($"Contact '{linkage}' does not exist.");
        }
        id = parsed;
        return null;
    }
}
