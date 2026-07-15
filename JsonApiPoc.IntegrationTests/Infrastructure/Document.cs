using System.Text.Json.Nodes;
using JsonApiKit.Testing;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>Builds the FULL expected JSON body for every response shape this API produces, for use
/// with ShouldMatchExactly. The document shapes come from JsonApiKit.Testing's
/// <see cref="JsonApiDocuments"/> golden-model builders; this class adds the per-resource
/// expectations (attribute sets in map order, relationship wiring) and converts this API's int ids
/// to the spec's string ids. Resource builders take the entities the test arranged — ids and
/// values come from what the test planted.</summary>
public static class Document
{
    // ── Resources (attribute order mirrors the maps) ────────────────────────────────────────────

    public static ResourceExpectation Company(Company company) =>
        new ResourceExpectation(ResourceTypes.Companies, company.Id.ToString(), $"{Routes.Companies}/{company.Id}")
            .Attr(Attr.Name, company.Name)
            .Attr(Attr.Industry, company.Industry)
            .Attr("website", company.Website)
            .RelatedOnlyRel(Rel.Contacts);

    public static ResourceExpectation Contact(Contact contact, object? customFields = null) =>
        new ResourceExpectation(ResourceTypes.Contacts, contact.Id.ToString(), $"{Routes.Contacts}/{contact.Id}")
            .Attr(Attr.FirstName, contact.FirstName)
            .Attr(Attr.LastName, contact.LastName)
            .Attr(Attr.Email, contact.Email)
            .Attr("phone", contact.Phone)
            .Attr(Attr.CustomFields, customFields)
            .ToOneRel(Rel.Company, ResourceTypes.Companies, contact.CompanyId.ToString());

    public static ResourceExpectation User(User user) =>
        new ResourceExpectation(ResourceTypes.Users, user.Id.ToString(), $"{Routes.Users}/{user.Id}")
            .Attr(Attr.Name, user.Name)
            .Attr(Attr.Email, user.Email);

    public static ResourceExpectation Deal(Deal deal, object? customFields = null) =>
        new ResourceExpectation(ResourceTypes.Deals, deal.Id.ToString(), $"{Routes.Deals}/{deal.Id}")
            .Attr(Attr.Title, deal.Title)
            .Attr(Attr.Amount, deal.Amount)
            .Attr(Attr.Stage, deal.Stage)
            .Attr("closeDate", Unspecified(deal.CloseDate))
            .Attr(Attr.CustomFields, customFields)
            .ToOneRel(Rel.Company, ResourceTypes.Companies, deal.CompanyId.ToString())
            .ToOneRel(Rel.Contact, ResourceTypes.Contacts, deal.ContactId?.ToString())
            .ToOneRel(Rel.Owner, ResourceTypes.Users, deal.OwnerId.ToString());

    public static ResourceExpectation Activity(Activity activity) =>
        new ResourceExpectation(ResourceTypes.Activities, activity.Id.ToString(), $"{Routes.Activities}/{activity.Id}")
            .Attr(Attr.Kind, activity.Kind)
            .Attr(Attr.Subject, activity.Subject)
            .Attr("dueAt", Unspecified(activity.DueAt))
            .Attr(Attr.Completed, activity.Completed)
            .ToOneRel(Rel.Deal, ResourceTypes.Deals, activity.DealId?.ToString())
            .ToOneRel(Rel.Contact, ResourceTypes.Contacts, activity.ContactId?.ToString());

    /// <summary>The only map without a self link, so the resource carries no links member.</summary>
    public static ResourceExpectation CustomField(CustomFieldDefinition definition) =>
        new ResourceExpectation(ResourceTypes.CustomFields, definition.Id.ToString(), selfLink: null)
            .Attr("resourceType", definition.ResourceType)
            .Attr(Attr.Key, definition.Key)
            .Attr("label", definition.Label)
            .Attr("dataType", definition.DataType);

    // ── Write request bodies (int-id conveniences over JsonApiDocuments) ────────────────────────

    /// <inheritdoc cref="JsonApiDocuments.Post"/>
    public static JsonNode Post(string type, object? attributes = null,
        params (string Name, string TargetType, int? TargetId)[] relationships) =>
        JsonApiDocuments.Post(type, attributes, StringIds(relationships));

    /// <inheritdoc cref="JsonApiDocuments.Patch"/>
    public static JsonNode Patch(string type, int id, object? attributes = null,
        params (string Name, string TargetType, int? TargetId)[] relationships) =>
        JsonApiDocuments.Patch(type, id.ToString(), attributes, StringIds(relationships));

    /// <inheritdoc cref="JsonApiDocuments.Linkage((string, string)?)"/>
    public static JsonNode Linkage((string Type, int Id)? identifier) =>
        JsonApiDocuments.Linkage(StringId(identifier));

    /// <inheritdoc cref="JsonApiDocuments.PostWithoutType"/>
    public static JsonNode PostWithoutType(object attributes) =>
        JsonApiDocuments.PostWithoutType(attributes);

    /// <inheritdoc cref="JsonApiDocuments.PostWithArrayData"/>
    public static JsonNode PostWithArrayData(string type, object attributes) =>
        JsonApiDocuments.PostWithArrayData(type, attributes);

    /// <inheritdoc cref="JsonApiDocuments.PostWithClientGeneratedId"/>
    public static JsonNode PostWithClientGeneratedId(string type, string id, object? attributes = null,
        params (string Name, string TargetType, int? TargetId)[] relationships) =>
        JsonApiDocuments.PostWithClientGeneratedId(type, id, attributes, StringIds(relationships));

    /// <inheritdoc cref="JsonApiDocuments.PatchWithDatalessRelationship"/>
    public static JsonNode PatchWithDatalessRelationship(string type, int id, string relationship) =>
        JsonApiDocuments.PatchWithDatalessRelationship(type, id.ToString(), relationship);

    // ── Documents ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="JsonApiDocuments.Single"/>
    public static JsonNode Single(ResourceExpectation resource, params ResourceExpectation[] included) =>
        JsonApiDocuments.Single(resource, included);

    /// <inheritdoc cref="JsonApiDocuments.Page"/>
    public static JsonNode Page(string path, string? query, int number, int size, int total,
        ResourceExpectation[] resources, params ResourceExpectation[] included) =>
        JsonApiDocuments.Page(path, query, number, size, total, resources, included);

    /// <inheritdoc cref="JsonApiDocuments.Related"/>
    public static JsonNode Related(string selfUrl, ResourceExpectation? resource) =>
        JsonApiDocuments.Related(selfUrl, resource);

    /// <inheritdoc cref="JsonApiDocuments.Linkage(string, string, (string, string)?)"/>
    public static JsonNode Linkage(string selfUrl, string relatedUrl, (string Type, int Id)? identifier) =>
        JsonApiDocuments.Linkage(selfUrl, relatedUrl, StringId(identifier));

    /// <inheritdoc cref="JsonApiDocuments.Problem"/>
    public static JsonNode Problem(int status, string title, string detail) =>
        JsonApiDocuments.Problem(status, title, detail);

    // ── Internals ───────────────────────────────────────────────────────────────────────────────

    private static (string Name, string TargetType, string? TargetId)[] StringIds(
        (string Name, string TargetType, int? TargetId)[] relationships) =>
        [.. relationships.Select(r => (r.Name, r.TargetType, r.TargetId?.ToString()))];

    private static (string Type, string Id)? StringId((string Type, int Id)? identifier)
    {
        if (identifier is { } linkage)
        {
            return (linkage.Type, linkage.Id.ToString());
        }
        return null;
    }

    /// <summary>Npgsql 'timestamp' columns round-trip DateTimes with Kind=Unspecified, so the
    /// server serializes them without a Z suffix; expected values must match that.</summary>
    private static DateTime? Unspecified(DateTime? value) =>
        value is { } dateTime ? DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified) : null;
}
