# JsonApiLite

Strongly typed [JSON:API](https://jsonapi.org/format/) request and response documents on
System.Text.Json. Targets net8.0 and net10.0.

It is the wire model and nothing else: no framework coupling, no validation pipeline, no HTTP.
Malformed input throws `JsonException`; the status code is yours to choose.

```
dotnet add package Simple.JsonApi
```

## Declare a resource

Attributes and relationships are records you own. The markers keep unrelated types out of those
positions; `IResourceType` names the resource type once, so a typo is a compile error.

```csharp
public sealed record ContactAttributes(string? FirstName, string? LastName) : IResourceType
{
    public static string ResourceType => "contacts";
}

public sealed record ContactRelationships : IRelationships
{
    public ToOneRelationship? Company { get; init; }
    public ToManyRelationship? Tags { get; init; }
}
```

`IAttributes`, `IRelationships` and `IMeta` are empty markers. `IResourceType` extends
`IAttributes`, so an attributes record that declares its type name already satisfies it;
implement `IAttributes` directly on one that does not.

## Write a document

```csharp
var document = new ResourceDocument<ContactAttributes, ContactRelationships>
{
    Data = new Resource<ContactAttributes, ContactRelationships>
    {
        Type = ContactAttributes.ResourceType,
        Id = "1",
        Attributes = new ContactAttributes("Ada", "Lovelace"),
        Relationships = new ContactRelationships
        {
            Company = Relationship.ToOne<CompanyAttributes>("7"),
            Tags = Relationship.ToMany<TagAttributes>(["3", "9"]),
        },
        Links = new Links { Self = "/contacts/1" },
    },
};

string json = JsonApiSerializer.Serialize(document);
```

```json
{"data":{"type":"contacts","id":"1",
  "attributes":{"firstName":"Ada","lastName":"Lovelace"},
  "relationships":{
    "company":{"data":{"type":"companies","id":"7"}},
    "tags":{"data":[{"type":"tags","id":"3"},{"type":"tags","id":"9"}]}},
  "links":{"self":"/contacts/1"}}}
```

Wrapped here for reading; the serializer emits one line.

## Read one back

```csharp
var received = JsonApiSerializer
    .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json)!;

string? first = received.Data!.Attributes!.FirstName;
ResourceIdentifier? company = received.Data.Relationships!.Company!.Data;
```

## The tri-state

In a write, *absent*, *null* and *a value* mean three different things:

| Wire | Relationship member | `Optional<T>` attribute | Meaning |
| --- | --- | --- | --- |
| member absent | `Company == null` | `Title.IsSet == false` | keep current value |
| `null` / `{"data":null}` | `Company.Data == null` | `IsSet == true, Value == null` | clear it |
| a value | `Company.Data` = identifier | `IsSet == true, Value` = value | set it |

`data` is always written, never dropped by null-omission; null members are always omitted. For
to-many, `"data": []` replaces the set with nothing. Relationships carry the distinction natively:

```csharp
new ContactRelationships
{
    Company = Relationship.ToOne<CompanyAttributes>("7"),  // set it
    Manager = Relationship.EmptyToOne(),                   // clear it  -> {"data":null}
    // Tags omitted                                        // keep current members
}
```

Reading a PATCH, the same three states come back as a null member, a member with null `Data`, and
a member with data:

```csharp
if (relationships.Manager is not null)      // the document carried it
{
    newManagerId = relationships.Manager.Data?.Id;   // null Data means "clear"
}
```

For attributes, plain nullable members treat null as "not sent". Where an explicit null must
reach the server, declare the member `Optional<T>`:

```csharp
public sealed record DealAttributes(Optional<string> Title, Optional<decimal?> Amount) : IResourceType
{
    public static string ResourceType => "deals";
}

var patch = new DealAttributes(Title: "Renewal", Amount: Optional<decimal?>.Of(null));
// title is set, amount is explicitly nulled, anything unset is omitted entirely

if (received.Amount.IsSet) { deal.Amount = received.Amount.Value; }   // else: leave alone
```

## Collections, pagination, meta

Meta has no members the spec reserves, so you declare its shape too and name it as the document's
third type parameter:

```csharp
public sealed record PageMeta(int Total, int PageCount) : IMeta;

var page = new ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>
{
    Data = [contact],
    Links = new Links { Self = "/contacts?page[number]=1", Next = "/contacts?page[number]=2" },
    Meta = new PageMeta(Total: 42, PageCount: 5),
};
```

Leave the parameter off and meta is the built-in `Meta`. In the positions that take no type
parameter — link, relationship, resource, identifier, error — name the shape at construction:

```csharp
public sealed record RoleMeta(string Role) : IMeta;

var company = Relationship.ToOne<CompanyAttributes>("7") with
{
    Meta = new Meta<RoleMeta>(new RoleMeta("primary")),
};

company.Meta!.As<RoleMeta>();      // back as a declared type
company.Meta!.Members["role"];     // or by name, for meta you have no type for
```

## Compound documents

`included` is heterogeneous, so it reads back as `Resource<JsonObject>` unless you register the
types:

```csharp
var options = JsonApiSerializer.CreateOptions(
    new ResourceTypeRegistry().Map<CompanyAttributes, CompanyRelationships>());

var document = JsonApiSerializer
    .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json, options)!;

var company = (Resource<CompanyAttributes, CompanyRelationships>)document.Included![0];
```

## Relationship endpoints

`/contacts/1/relationships/company` bodies are their own documents:

```csharp
new ToOneLinkageDocument { Data = null };                                   // clear
new ToOneLinkageDocument { Data = ResourceIdentifier.Of<CompanyAttributes>("8") };
new ToManyLinkageDocument { Data = [ResourceIdentifier.Of<TagAttributes>("3")] };
```

## Errors

```csharp
var problem = new ErrorDocument
{
    Errors =
    [
        new Error
        {
            Status = "422",
            Title = "Validation failed",
            Detail = "The title attribute is required.",
            Source = new ErrorSource { Pointer = "/data/attributes/title" },
        },
    ],
};
```

## Content type

```csharp
JsonApiMediaType.Value   // "application/vnd.api+json"
```

## Also in the box

- `Resource<TAttributes>` — relationships keyed by name, the escape hatch when they are not known
  at compile time, with `ToOne(name)` / `ToMany(name)` lookups. Also what `included` reads back as
  (`Resource<JsonObject>`) without a registry.
- `LinksRelationship` — a relationship carrying links or meta but no linkage, which servers emit
  when the linkage itself is not included.
- `JsonApiObject` — the top-level `jsonapi` member: `version`, `ext`, `profile`, `meta`.
- `Link` — a bare URI string, or `{href, meta}` when it carries meta.
- `Meta.Members` — the members as sent, for meta you have no declared type for.

## Tests as documentation

`libs/tests/JsonApiLite.Tests` (76 tests) mirrors the source folders where a subject owns a file:
`Serialization/` pins single features to exact wire JSON, `Documents/` climbs from rich single
documents to compound ones, `Relationships/` covers both arities. Cross-cutting files sit at the
root, as `Optional` and `JsonApiMediaType` do in the source: `OptionalAttribute`,
`SpecCompliance`, and `RequestResponseScenario` whole client↔server cycles. Valid documents are
built as objects and round-tripped; raw JSON appears only as expected output or as
`JsonObject`-built protocol violations. Method names are the spec, bodies the proof.

## A sample that consumes the package

[`JsonApiPoc.Api`](JsonApiPoc.Api) is a minimal API over in-memory mock data, referencing
`Simple.JsonApi` from nuget.org rather than the source next to it — the library is exercised the
way an outside caller would. It covers a collection with pagination links and meta, a single
resource with `include`, create returning 201 and a `Location`, a PATCH demonstrating the
tri-state, delete, the relationship and related endpoints, and 404/422 as error documents.
`JsonApi.cs` is the entire ASP.NET seam, since the library models documents and not HTTP.

It is a demonstration, not a reference implementation: storage is a static list, `include` accepts
one value, query parameters are hand-parsed, there is no content negotiation and no auth, and
nothing is tested. Read it for endpoint shapes, not for anything to run.

## Where this does not cover the specification

The library models documents. Everything the spec says about *transport* is out of scope by
design, and a few document-level features are genuinely missing.

**Not implemented**

- **`lid`.** JSON:API 1.1 lets a client-created resource carry a `lid` in place of `id`, so one
  document can reference a resource that does not exist server-side yet. `Resource` and
  `ResourceIdentifier` model `id` only, so create-with-linkage in a single document cannot be
  expressed.
- **Atomic operations.** The extension is not supported; `JsonApiObject.Ext` can declare a URI,
  but nothing implements the operations format.
- **Query parameters.** `include`, `fields`, `filter`, `sort` and `page` are spec-defined and
  entirely absent — no parsing, no building. Pagination appears only as `Links` you populate.
- **Extension and profile negotiation.** `ext` and `profile` round-trip inside the `jsonapi`
  object, but nothing negotiates them or enforces their rules.

**Deliberately absent**

- **HTTP.** Content negotiation, status codes, the `Location` header on 201, and the `406`/`415`
  responses are the caller's. `JsonApiMediaType.Value` is the only concession.
- **Validation.** Documents are not checked against the spec beyond what parsing requires. A
  relationship with none of `data`/`links`/`meta` is rejected and a declared arity mismatch is
  rejected, but member requirements are otherwise not enforced — a resource identifier arriving
  without an `id`, for instance, is not caught.

**Known limitation**

- Typing meta requires the typed-relationships document. `ResourceDocument<TAttributes, TMeta>`
  cannot exist alongside `ResourceDocument<TAttributes, TRelationships>`, because C# identifies a
  generic type by name and arity alone — constraints are not part of that identity. Use
  `ResourceDocument<TAttributes, TRelationships, TMeta>`.

## License

[MIT](LICENSE).
