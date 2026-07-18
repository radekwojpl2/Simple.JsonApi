# JsonApiLite

Minimal, strongly typed [JSON:API](https://jsonapi.org/format/) request and response documents on
System.Text.Json. No framework coupling, no validation pipeline, no mapping layer — just the wire
model. Malformed input surfaces as `JsonException`; what status that draws is the caller's call.

## The model

| Type | Wire shape |
| --- | --- |
| `ResourceDocument<TAttributes, TRelationships>` | `{ "data": { resource } }` — create/update bodies, single-resource responses |
| `ResourceCollectionDocument<TAttributes, TRelationships>` | `{ "data": [ resources ] }` — list responses |
| `Resource<TAttributes, TRelationships>` | `type`/`id`/`attributes`/`relationships`/`links`, both halves typed |
| `Resource<TAttributes>` (and 1-param documents) | relationships as a name-keyed dictionary — the escape hatch when names aren't known at compile time |
| `Resource` (abstract) | element type of `included`, where resources are heterogeneous |
| `Meta` | the pagination meta responses carry (`total`, `pageCount`) |
| `ToOneRelationship` / `ToManyRelationship` | relationship object; read polymorphically from whether `data` is an array |
| `LinksRelationship` | spec-valid relationship object with links but no `data` (responses from other servers) |
| `ToOneLinkageDocument` / `ToManyLinkageDocument` | `{ "data": identifier-or-null }` — relationship endpoints |
| `ErrorDocument` / `Error` | `{ "errors": [...] }` with the full spec surface (`id`, `status`, `code`, `title`, `detail`, `source`, `links.about`, `meta`) |
| `Link` | a bare URI string or a `{ "href": ..., "meta": ... }` link object; implicitly converts from string |
| `Optional<T>` | opt-in attribute tri-state: absent vs. explicit null vs. value |
| `ResourceTypeRegistry` | maps resource type names so `included` deserializes strongly typed |
| `IResourceType` | opt-in: declare the resource type name once on the attributes record; use it via `Relationship.ToOne<T>(id)`, `Resource.Create<T, TRel>(...)`, `ResourceIdentifier.Of<T>(id)`, `Map<T, TRel>()` |

Serialize and deserialize through `JsonApiSerializer` (or pass `JsonApiSerializer.Options` to your
framework). The media type constant lives in `JsonApiMediaType.Value`.

Relationships are declared once per resource as a record whose members are nullable
`ToOneRelationship`/`ToManyRelationship` properties, and the resource type name can be declared
once on the attributes record (spec names like "contacts" are not derivable from CLR names, so
they are declared, not convention-derived):

```csharp
public sealed record ContactAttributes(string? FirstName, string? LastName) : IResourceType
{
    public static string ResourceType => "contacts";
}

public sealed record ContactRelationships
{
    public ToOneRelationship? Company { get; init; }   // targets set via ToOne<CompanyAttributes>(id)
    public ToManyRelationship? Tags { get; init; }
}
```

## Semantics kept on the type level

- A relationship the document doesn't carry is a **null member** (or a name absent from the
  dictionary flavor) and means "keep the current value"; a `ToOneRelationship` with **null
  `Data`** means "clear it". `data` is always written, never dropped by null-omission, while null
  relationship members are omitted.
- The declared member type drives read-side **arity checking**: an identifier array arriving
  where `ToOneRelationship` is declared is rejected as malformed JSON (dictionary flavor:
  `ToOne(name)`/`ToMany(name)` check arity on access).
- `Attributes` is null when the document carried no attributes member — the partial-update
  "missing attributes keep their current values" rule in one null check. For *individual*
  attributes where an explicit null is meaningful, declare the member as `Optional<T>`:
  `attributes.Title.IsSet` distinguishes "not sent" from "set to null", and unset members are
  omitted when writing.
- Reading someone else's compound documents strongly typed: pass
  `JsonApiSerializer.CreateOptions(new ResourceTypeRegistry().Map<CompanyAttributes, CompanyRelationships>("companies"))`
  and mapped `included` resources deserialize as their concrete types; unmapped ones fall back to
  `Resource<JsonObject>`.
- `Included` holds any mix of `Resource<TIncluded>` values and writes each with its concrete
  attributes type. Reading a compound document yields `Resource<JsonObject>` elements — the
  resource's `Type` says which attributes type to deserialize the `JsonObject` into.

## Writing a response

```csharp
var document = new ResourceDocument<ContactAttributes, ContactRelationships>
{
    Data = new Resource<ContactAttributes, ContactRelationships>
    {
        Type = "contacts",
        Id = "1",
        Attributes = new ContactAttributes("Ada", "Lovelace"),
        Relationships = new ContactRelationships
        {
            Company = Relationship.ToOne("companies", "7"),
        },
        Links = new Links { Self = "/contacts/1" },
    },
};
var json = JsonApiSerializer.Serialize(document);
```

## Reading a request

```csharp
var document = JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json);
var attributes = document?.Data?.Attributes;                  // null: no attributes member
var company = document?.Data?.Relationships?.Company;         // null: relationship not sent
var target = company?.Data;                                   // null: clear the relationship
```
