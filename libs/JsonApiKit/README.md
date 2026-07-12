# JsonApiKit

Reusable [JSON:API](https://jsonapi.org/format/) building blocks for ASP.NET Core **minimal APIs**: a document model, resource maps, validated query-parameter binding, pagination links, and pluggable error formatting.

JsonApiKit is deliberately *not* a framework. It doesn't own your routes, your data access, or your controllers — you write ordinary minimal-API endpoints and use the kit to parse the JSON:API query string and produce spec-correct response documents. There is no reflection or expression-tree magic: resource maps are plain delegates, so everything is fast, debuggable, and AOT-friendly.

## Packages

| Package | What it adds | Dependencies |
|---|---|---|
| `JsonApiKit` | Core: documents, maps, query binding, pagination, errors | none (framework reference only) |
| `JsonApiKit.OpenApi` | Documents the JSON:API query parameters in generated OpenAPI documents | `Microsoft.AspNetCore.OpenApi` |

The OpenAPI integration lives in a separate package so the core library carries no package dependencies at all.

## Quick start

**1. Describe how an entity serializes with a `ResourceMap<T>`:**

```csharp
public sealed class WidgetMap : ResourceMap<Widget>
{
    public override string ResourceType => "widgets";
    protected override string Id(Widget w) => w.Id.ToString();

    public WidgetMap()
    {
        SelfLink(w => $"/api/widgets/{w.Id}");
        Attribute("name", w => w.Name);
        Attribute("note", w => w.Note);                          // null values are omitted
        ToOne("owner", "users", w => w.OwnerId, links: true);    // null id omits the relationship
        ToMany("tags", "tags", w => w.TagIds.Cast<object>());
        ToManyLinks("comments");                                 // links-only: a related link, no linkage data
    }
}
```

The attribute/relationship names double as the spec's "fields", so sparse fieldsets (`?fields[widgets]=name,owner`) are validated against them automatically.

**2. Register the kit:**

```csharp
builder.Services.AddJsonApi(o => o.AddMap<WidgetMap>());

var app = builder.Build();
app.UseExceptionHandler();          // required: translates query-binding errors into 400 responses
app.UseJsonApiContentNegotiation(); // spec's 415/406 media-type parameter rules
```

**3. Write an endpoint.** Declare `JsonApiQuery` as a handler parameter — it binds from the request and validates against the endpoint's declared policy:

```csharp
app.MapGet("/api/widgets", async (JsonApiQuery query, WidgetService service, ResourceMapRegistry maps) =>
{
    var (widgets, total) = await service.List(query.Page, query.Sort, query.Filter("search"));
    var map = maps.Get<Widget>();

    return JsonApiResults.Ok(new JsonApiDocument
    {
        Data = widgets.Select(w => map.Build(w, query)).ToList(),
        Links = query.PageLinks(total),
        Meta = query.PageMeta(total)
    });
})
.WithJsonApiQuery(
    includes: ["owner"],
    sorts: ["name"],
    filters: new() { ["search"] = null },     // null = any value accepted
    fieldsFor: ["widgets"]);
```

Anything the endpoint didn't declare — an unknown `include` path, sort field, filter name, or (per the [spec's query-parameter rules](https://jsonapi.org/format/#query-parameters)) any unrecognized `page[...]`/`fields`/`filter` usage — is rejected with a 400 whose `source.parameter` names the offending key and whose detail lists the supported values.

## Concepts

### Query binding — `JsonApiQuery`

`JsonApiQuery.BindAsync` reads the endpoint's `JsonApiQueryOptions` metadata (attached by `WithJsonApiQuery`) and the global `JsonApiOptions`, then parses and validates:

| Parameter | Surface | Notes |
|---|---|---|
| `include=a,b` | `query.Has("a")`, `query.Includes` | validated against `includes` allowlist |
| `sort=-amount,name` | `query.Sort` — `(Field, Descending)` in request order | validated against `sorts` |
| `page[number]`, `page[size]` | `query.Page` | 1-based; size capped by `MaxPageSize` (default 100) |
| `filter[name]=value` | `query.Filter("name")` | declared names only, optionally with a value allowlist |
| `fields[type]=a,b` | `query.Fields("type")` | validated against the registered map's fields |

`Strict = true` (the default) also rejects query parameters the endpoint doesn't understand; set `strict: false` to ignore them. Reserved families (`include`, `sort`, `page`, `fields`, `filter`) are validated regardless, as the spec requires.

Invalid input throws `JsonApiQueryException`, which the registered `IExceptionHandler` turns into a formatted 400 — **this is why the app must call `UseExceptionHandler()`**.

### Pagination

`query.PageLinks(total)` builds spec pagination links (`self`/`first`/`prev`/`next`/`last`), preserving every other query parameter in the request, clamping out-of-range pages, and treating an empty collection as one page so `first`/`last` always exist. `query.PageMeta(total)` yields the conventional `{ total, pageCount }` meta. The links are derived from the current request path, so they work unchanged on a nested related-resource route such as `/api/companies/1/contacts`.

### Relationships

`ToOne` and `ToMany` emit [resource linkage](https://jsonapi.org/format/#document-resource-object-linkage) — a `{type, id}` object, or an array of them. A null to-one id omits the relationship entirely; an empty to-many emits `"data": []`.

`ToManyLinks(name)` declares a to-many that emits **no linkage at all**, only a `related` link pointing at the collection endpoint:

```json
"contacts": { "links": { "related": "/api/companies/1/contacts" } }
```

The spec permits omitting `data`, and doing so keeps a collection response from loading every related id for every resource in it. It emits `related` alone — no `self` — because the kit does not assume you serve a `/relationships/{name}` route; declare a link only for a URL the API actually answers. Like the other link helpers it derives from `SelfLink`, and the relationship is omitted from the resource object when no `SelfLink` is declared (a relationship object carrying neither links nor data is invalid). Both members of `RelationshipLinks` are therefore optional.

### Documents and results

`JsonApiDocument`, `ResourceObject`, `Relationship`, `JsonApiToOneDocument`, etc. model the wire format; null members are omitted by `JsonApiResults.SerializerOptions` (camelCase, `WhenWritingNull`). Helpers return results with the `application/vnd.api+json` media type:

- `JsonApiResults.Ok(document)` — 200
- `JsonApiResults.Created(location, document)` — 201 + `Location` header, per the [creation rules](https://jsonapi.org/format/#crud-creating-responses-201)
- `JsonApiResults.Result(document, statusCode)` — anything else

`JsonApiToOneDocument` exists for relationship endpoints: it serializes `"data": null` explicitly for an empty to-one, which the default null-omitting options would otherwise drop.

### Errors

All errors — hand-written and binding — render as RFC 7807 problem details (`application/problem+json`), consistent with the framework-generated errors of an ASP.NET Core app. Endpoints write their own through static helpers on `JsonApiResults`:

- `JsonApiResults.NotFound(detail)` — 404
- `JsonApiResults.Validation(detail)` — 422
- `JsonApiResults.BadRequest(title, detail)` — 400
- `JsonApiResults.Error(jsonApiError)` — any status

### Content negotiation

`app.UseJsonApiContentNegotiation()` enforces the spec's server responsibilities ([§ content negotiation](https://jsonapi.org/format/#content-negotiation-servers)): a request whose `Content-Type` is the JSON:API media type modified by disallowed parameters gets **415**, and a request whose `Accept` offers the JSON:API media type only in modified instances gets **406**. `profile` is allowed (unrecognized profiles are ignored), `ext` is rejected because the kit supports no extensions, and `q` is exempt as HTTP's quality weight rather than a media type parameter.

### OpenAPI (optional — `JsonApiKit.OpenApi`)

Because `JsonApiQuery` binds via `BindAsync`, its query parameters are invisible to OpenAPI generation. The companion package documents them from the same `WithJsonApiQuery` metadata that drives validation:

```csharp
builder.Services.AddOpenApi(o => o.AddJsonApiQueryParameters());
```

Each operation gains `include`, `sort`, `page[number]`, `page[size]`, `filter[...]`, and `fields[...]` parameters, with allowed values (and, for fieldsets, the map's declared fields) in the descriptions.

## Scope — what JsonApiKit does *not* do

- **Request-body parsing.** Incoming POST/PATCH resource documents are your endpoint's job; the kit only produces responses and parses the query string. The one exception is `ToOneLinkage.TryParse`, which parses the spec's to-one relationship update body (`{"data": {"type","id"}}` or `{"data": null}`) into a target id or a ready-made 400/409 error result.
- **Compound-document assembly.** `include` is validated, but building the `included` array (and deduplicating by type/id) is done in the endpoint.
- **Query execution.** `Sort` and `Filter` give you validated values; translating them to your data layer is up to you.

## Testing

The `JsonApiKit.Tests` project covers parsing, binding, maps, serialization, pagination, results, error formatting, DI registration, and the OpenAPI transformer. Run with `dotnet test`.
