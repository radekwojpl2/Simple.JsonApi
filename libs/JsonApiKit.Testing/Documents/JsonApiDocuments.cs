using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonApiKit.Testing;

/// <summary>Builders for JSON:API request bodies and FULL expected response documents, for use
/// with ShouldMatchExactly. The expected documents form a golden model: they re-encode the wire
/// contract (attribute sets, relationship links, pagination links, omission rules) independently
/// of the server's production mapping code, so a mapping bug cannot hide inside the expectation.
/// Ids are strings, as the spec defines them; a test suite for a server with numeric keys
/// typically wraps these builders with converting overloads.</summary>
public static class JsonApiDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ── Write request bodies (POST/PATCH — JSON:API has no PUT) ─────────────────────────────────

    /// <summary>POST body: a JSON:API resource document creating a <paramref name="type"/>
    /// resource. <paramref name="attributes"/> is an anonymous object serialized with web JSON
    /// naming; each entry of <paramref name="relationships"/> becomes a to-one linkage, with a
    /// null id emitting an explicit data: null.</summary>
    public static JsonNode Post(string type, object? attributes = null,
        params (string Name, string TargetType, string? TargetId)[] relationships) =>
        WriteDocument(type, id: null, attributes, relationships);

    /// <summary>PATCH body: a JSON:API resource document updating <paramref name="type"/>/
    /// <paramref name="id"/> (the document's id member must match the URL). Omitted attributes and
    /// relationships keep their current values; a relationship with a null id clears it.</summary>
    public static JsonNode Patch(string type, string id, object? attributes = null,
        params (string Name, string TargetType, string? TargetId)[] relationships) =>
        WriteDocument(type, id, attributes, relationships);

    /// <summary>PATCH body for a /relationships/{name} endpoint: a to-one linkage document whose
    /// data is the identifier, or an explicit data: null to clear the relationship.</summary>
    public static JsonNode Linkage((string Type, string Id)? identifier)
    {
        JsonNode? data = null;
        if (identifier is { } linkage)
        {
            data = new JsonObject { ["type"] = linkage.Type, ["id"] = linkage.Id };
        }
        return new JsonObject { ["data"] = data };
    }

    // ── Deliberately non-conformant bodies, for the spec's rejection tests. The regular builders
    //    never produce these shapes, so they get their own explicitly named factories. ──────────

    /// <summary>A resource object without the type member the spec requires -> 400.</summary>
    public static JsonNode PostWithoutType(object attributes) => new JsonObject
    {
        ["data"] = new JsonObject { ["attributes"] = ToNode(attributes) }
    };

    /// <summary>An array as primary data where a create needs a single resource object -> 400.</summary>
    public static JsonNode PostWithArrayData(string type, object attributes) => new JsonObject
    {
        ["data"] = new JsonArray(new JsonObject
        {
            ["type"] = type,
            ["attributes"] = ToNode(attributes)
        })
    };

    /// <summary>A create carrying a client-generated id -> 403 from servers that assign ids;
    /// <see cref="Post"/> has no id parameter for that reason.</summary>
    public static JsonNode PostWithClientGeneratedId(string type, string id, object? attributes = null,
        params (string Name, string TargetType, string? TargetId)[] relationships) =>
        WriteDocument(type, id, attributes, relationships);

    /// <summary>An update whose relationship entry is not a relationship object with a data
    /// member -> 400.</summary>
    public static JsonNode PatchWithDatalessRelationship(string type, string id, string relationship)
    {
        var document = WriteDocument(type, id, attributes: null, []);
        document["data"]!["relationships"] = new JsonObject { [relationship] = new JsonObject() };
        return document;
    }

    private static JsonNode WriteDocument(string type, string? id, object? attributes,
        (string Name, string TargetType, string? TargetId)[] relationships)
    {
        var resource = new JsonObject { ["type"] = type };
        if (id is not null)
        {
            resource["id"] = id;
        }
        if (attributes is not null)
        {
            resource["attributes"] = ToNode(attributes);
        }
        if (relationships.Length > 0)
        {
            var relationshipsObject = new JsonObject();
            foreach (var (name, targetType, targetId) in relationships)
            {
                JsonNode? data = null;
                if (targetId is { } linkedId)
                {
                    data = new JsonObject { ["type"] = targetType, ["id"] = linkedId };
                }
                relationshipsObject[name] = new JsonObject { ["data"] = data };
            }
            resource["relationships"] = relationshipsObject;
        }
        return new JsonObject { ["data"] = resource };
    }

    // ── Expected response documents ─────────────────────────────────────────────────────────────

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
    public static JsonNode Linkage(string selfUrl, string relatedUrl, (string Type, string Id)? identifier) =>
        new JsonObject
        {
            ["data"] = identifier is { } linkage
                ? new JsonObject { ["type"] = linkage.Type, ["id"] = linkage.Id }
                : null,
            ["links"] = new JsonObject { ["self"] = selfUrl, ["related"] = relatedUrl }
        };

    /// <summary>Full RFC 7807 problem-details body: exactly type, title, status, detail, with the
    /// default type URIs ASP.NET's ProblemDetailsDefaults assigns per status.</summary>
    public static JsonNode Problem(int status, string title, string detail) => new JsonObject
    {
        ["type"] = status switch
        {
            400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
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

    internal static JsonNode? ToNode(object? value) => JsonSerializer.SerializeToNode(value, Web);
}
