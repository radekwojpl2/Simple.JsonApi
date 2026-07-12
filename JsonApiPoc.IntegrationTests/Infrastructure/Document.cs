using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>Builds the FULL expected JSON body for every response shape this API produces, for use
/// with ShouldMatchExactly. This is a golden model: it re-encodes the API's wire contract
/// (attribute sets, relationship links, pagination links, omission rules) independently of the
/// production resource maps, so a mapping bug cannot hide inside the expectation. Resource
/// builders take the entities the test arranged — ids and values come from what the test planted.</summary>
public static class Document
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ── Resources (attribute order mirrors the maps) ────────────────────────────────────────────

    public static ResourceExpectation Company(Company company) =>
        new ResourceExpectation(ResourceTypes.Companies, company.Id, $"{Routes.Companies}/{company.Id}")
            .Attr(Attr.Name, company.Name)
            .Attr(Attr.Industry, company.Industry)
            .Attr("website", company.Website)
            .RelatedOnlyRel(Rel.Contacts);

    public static ResourceExpectation Contact(Contact contact, object? customFields = null) =>
        new ResourceExpectation(ResourceTypes.Contacts, contact.Id, $"{Routes.Contacts}/{contact.Id}")
            .Attr(Attr.FirstName, contact.FirstName)
            .Attr(Attr.LastName, contact.LastName)
            .Attr(Attr.Email, contact.Email)
            .Attr("phone", contact.Phone)
            .Attr(Attr.CustomFields, customFields)
            .ToOneRel(Rel.Company, ResourceTypes.Companies, contact.CompanyId);

    public static ResourceExpectation User(User user) =>
        new ResourceExpectation(ResourceTypes.Users, user.Id, $"{Routes.Users}/{user.Id}")
            .Attr(Attr.Name, user.Name)
            .Attr(Attr.Email, user.Email);

    public static ResourceExpectation Deal(Deal deal, object? customFields = null) =>
        new ResourceExpectation(ResourceTypes.Deals, deal.Id, $"{Routes.Deals}/{deal.Id}")
            .Attr(Attr.Title, deal.Title)
            .Attr(Attr.Amount, deal.Amount)
            .Attr(Attr.Stage, deal.Stage)
            .Attr("closeDate", Unspecified(deal.CloseDate))
            .Attr(Attr.CustomFields, customFields)
            .ToOneRel(Rel.Company, ResourceTypes.Companies, deal.CompanyId)
            .ToOneRel(Rel.Contact, ResourceTypes.Contacts, deal.ContactId)
            .ToOneRel(Rel.Owner, ResourceTypes.Users, deal.OwnerId);

    public static ResourceExpectation Activity(Activity activity) =>
        new ResourceExpectation(ResourceTypes.Activities, activity.Id, $"{Routes.Activities}/{activity.Id}")
            .Attr(Attr.Kind, activity.Kind)
            .Attr(Attr.Subject, activity.Subject)
            .Attr("dueAt", Unspecified(activity.DueAt))
            .Attr(Attr.Completed, activity.Completed)
            .ToOneRel(Rel.Deal, ResourceTypes.Deals, activity.DealId)
            .ToOneRel(Rel.Contact, ResourceTypes.Contacts, activity.ContactId);

    /// <summary>The only map without a self link, so the resource carries no links member.</summary>
    public static ResourceExpectation CustomField(CustomFieldDefinition definition) =>
        new ResourceExpectation(ResourceTypes.CustomFields, definition.Id, selfLink: null)
            .Attr("resourceType", definition.ResourceType)
            .Attr(Attr.Key, definition.Key)
            .Attr("label", definition.Label)
            .Attr("dataType", definition.DataType);

    // ── Documents ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Single-resource document: data only, no top-level links or meta. Also the shape of
    /// a 201 Created body. Pass <paramref name="included"/> for compound single-resource docs.</summary>
    public static JsonNode Single(ResourceExpectation resource, params ResourceExpectation[] included)
    {
        var document = new JsonObject { ["data"] = resource.Build() };
        if (included.Length > 0)
        {
            document["included"] = new JsonArray(included.Select(r => r.Build()).ToArray());
        }
        return document;
    }

    /// <summary>Collection document with full pagination links and meta. <paramref name="query"/>
    /// is the request's non-page query string ("sort=-amount&amp;filter[stage]=won") or null; page
    /// links preserve it and append the encoded page parameters, mirroring JsonApiQuery.Url.</summary>
    public static JsonNode Page(string path, string? query, int number, int size, int total,
        ResourceExpectation[] resources, params ResourceExpectation[] included)
    {
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var links = new JsonObject { ["self"] = Url(path, query, number, size) };
        links["first"] = Url(path, query, 1, size);
        if (number > 1)
        {
            links["prev"] = Url(path, query, Math.Min(number - 1, pageCount), size);
        }
        if (number < pageCount)
        {
            links["next"] = Url(path, query, number + 1, size);
        }
        links["last"] = Url(path, query, pageCount, size);

        var document = new JsonObject
        {
            ["data"] = new JsonArray(resources.Select(r => r.Build()).ToArray())
        };
        if (included.Length > 0)
        {
            document["included"] = new JsonArray(included.Select(r => r.Build()).ToArray());
        }
        document["links"] = links;
        document["meta"] = new JsonObject { ["total"] = total, ["pageCount"] = pageCount };
        return document;
    }

    /// <summary>Related to-one document: data (a full resource, or explicit null) + links.self.</summary>
    public static JsonNode Related(string selfUrl, ResourceExpectation? resource) => new JsonObject
    {
        ["data"] = resource?.Build(),
        ["links"] = new JsonObject { ["self"] = selfUrl }
    };

    /// <summary>Relationship linkage document: data ({type,id} or explicit null) + links.self/related.</summary>
    public static JsonNode Linkage(string selfUrl, string relatedUrl, (string Type, int Id)? identifier) =>
        new JsonObject
        {
            ["data"] = identifier is { } linkage
                ? new JsonObject { ["type"] = linkage.Type, ["id"] = linkage.Id.ToString() }
                : null,
            ["links"] = new JsonObject { ["self"] = selfUrl, ["related"] = relatedUrl }
        };

    /// <summary>Full RFC 7807 problem-details body: exactly type, title, status, detail.</summary>
    public static JsonNode Problem(int status, string title, string detail) => new JsonObject
    {
        ["type"] = status switch
        {
            400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            // ASP.NET's ProblemDetailsDefaults maps 422 to the WebDAV RFC, not RFC 9110.
            422 => "https://tools.ietf.org/html/rfc4918#section-11.2",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Add the default type URI.")
        },
        ["title"] = title,
        ["status"] = status,
        ["detail"] = detail
    };

    // ── Internals ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Mirrors JsonApiQuery.Url: original query parameters in order (re-encoded the way
    /// QueryBuilder does), then page[number] and page[size].</summary>
    private static string Url(string path, string? query, int number, int size)
    {
        var pairs = new List<string>();
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=', 2);
                pairs.Add($"{Encode(parts[0])}={Encode(parts.Length > 1 ? parts[1] : "")}");
            }
        }
        pairs.Add($"page%5Bnumber%5D={number}");
        pairs.Add($"page%5Bsize%5D={size}");
        return $"{path}?{string.Join("&", pairs)}";
    }

    private static string Encode(string value) => UrlEncoder.Default.Encode(value);

    /// <summary>Npgsql 'timestamp' columns round-trip DateTimes with Kind=Unspecified, so the
    /// server serializes them without a Z suffix; expected values must match that.</summary>
    private static DateTime? Unspecified(DateTime? value) =>
        value is { } dateTime ? DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified) : null;

    internal static JsonNode? ToNode(object? value) => JsonSerializer.SerializeToNode(value, Web);
}

/// <summary>One expected resource object: type/id, attributes and relationships in map order, and
/// links.self. Null attribute values and null to-one FKs are omitted, mirroring the server's
/// WhenWritingNull serialization and ResourceMap skip rules.</summary>
public sealed class ResourceExpectation(string type, int id, string? selfLink)
{
    private readonly List<(string Name, object? Value)> _attributes = [];
    private readonly List<(string Name, JsonObject Relationship)> _relationships = [];

    internal ResourceExpectation Attr(string name, object? value)
    {
        _attributes.Add((name, value));
        return this;
    }

    /// <summary>To-one with self/related links and linkage data; skipped when the FK is null.</summary>
    internal ResourceExpectation ToOneRel(string name, string targetType, int? targetId)
    {
        if (targetId is { } linkedId)
        {
            _relationships.Add((name, new JsonObject
            {
                ["links"] = new JsonObject
                {
                    ["self"] = $"{selfLink}/relationships/{name}",
                    ["related"] = $"{selfLink}/{name}"
                },
                ["data"] = new JsonObject { ["type"] = targetType, ["id"] = linkedId.ToString() }
            }));
        }
        return this;
    }

    /// <summary>Links-only to-many: a related link, no linkage data.</summary>
    internal ResourceExpectation RelatedOnlyRel(string name)
    {
        _relationships.Add((name, new JsonObject
        {
            ["links"] = new JsonObject { ["related"] = $"{selfLink}/{name}" }
        }));
        return this;
    }

    /// <summary>Sparse fieldset: keep only the named attributes and relationships; links.self
    /// survives, mirroring ResourceMap.Build.</summary>
    public ResourceExpectation Fields(params string[] fields)
    {
        _attributes.RemoveAll(attribute => !fields.Contains(attribute.Name));
        _relationships.RemoveAll(relationship => !fields.Contains(relationship.Name));
        return this;
    }

    internal JsonNode Build()
    {
        var resource = new JsonObject { ["type"] = type, ["id"] = id.ToString() };

        var attributes = new JsonObject();
        foreach (var (name, value) in _attributes)
        {
            if (value is not null)
            {
                attributes[name] = Document.ToNode(value);
            }
        }
        if (attributes.Count > 0)
        {
            resource["attributes"] = attributes;
        }

        if (_relationships.Count > 0)
        {
            var relationships = new JsonObject();
            foreach (var (name, relationship) in _relationships)
            {
                relationships[name] = relationship.DeepClone();
            }
            resource["relationships"] = relationships;
        }

        if (selfLink is not null)
        {
            resource["links"] = new JsonObject { ["self"] = selfLink };
        }
        return resource;
    }
}
