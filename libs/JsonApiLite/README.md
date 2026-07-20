# JsonApiLite

Minimal, strongly typed [JSON:API](https://jsonapi.org/format/) request/response documents on
System.Text.Json. No framework coupling, no validation pipeline — just the wire model. Malformed
input throws `JsonException`; the HTTP status is the caller's call.

## Declaring a resource

```csharp
public sealed record ContactAttributes(string? FirstName, string? LastName) : IResourceType
{
    public static string ResourceType => "contacts";   // declared once, referenced by type
}

public sealed record ContactRelationships
{
    public ToOneRelationship? Company { get; init; }
    public ToManyRelationship? Tags { get; init; }
}
```

## Writing and reading

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
        },
        Links = new Links { Self = "/contacts/1" },
    },
};
var json = JsonApiSerializer.Serialize(document);
var back = JsonApiSerializer.Deserialize<ResourceDocument<ContactAttributes, ContactRelationships>>(json);
```

## The tri-state

In JSON:API writes, *absent*, *null*, and *a value* mean different things. The types keep all
three distinct:

| Wire | Relationship member | `Optional<T>` attribute | Meaning |
| --- | --- | --- | --- |
| member absent | `Company == null` | `Title.IsSet == false` | keep current value |
| `null` / `{"data":null}` | `Company.Data == null` | `IsSet == true, Value == null` | clear it |
| a value | `Company.Data` = identifier | `IsSet == true, Value` = value | set it |

`data` is always written (never dropped by null-omission); null members are always omitted.
To-many: `"data": []` replaces the set with nothing. Plain nullable attributes stay fine when
explicit null never matters.

## Also in the box

- `ToOneLinkageDocument` / `ToManyLinkageDocument` — `.../relationships/{name}` endpoint bodies.
- `ErrorDocument` / `Error` — full spec error surface; `Link` — string or `{href, meta}` object.
- `Resource<TAttributes>` — dictionary relationships, the escape hatch for unknown names; also
  what `included` reads back as (`Resource<JsonObject>`), unless a `ResourceTypeRegistry` via
  `JsonApiSerializer.CreateOptions(...)` maps types to concrete resources.
- `LinksRelationship` — spec-valid links-only relationship (reading other servers' responses).
- Media type: `JsonApiMediaType.Value`.

## Typed meta

The spec reserves no meta member names — meta is as user-defined as `attributes` — so nothing is
typed in advance. Documents take the shape as a type parameter:

```csharp
public sealed record PageMeta(int? Total, string? GeneratedAt);

new ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>
{
    Data = [],
    Meta = new PageMeta(Total: 2, GeneratedAt: "2026-07-20"),
};
```

Everywhere else — link, relationship and error meta — the shape is named at construction with
`Meta<T>`:

```csharp
Meta = new Meta<PageMeta>(new PageMeta(Total: 2, GeneratedAt: "2026-07-20")),
```

Those positions store the base `Meta` rather than a type parameter of their own, because a
parameter on `Link` would cascade into `Links` and every type holding one. That also means the
wire carries no type name, so meta read back off the wire is the base type — recover the shape
with `As<T>()`, or reach for members by name when there is no shape to recover:

```csharp
document.Meta!.As<PageMeta>()            // as a declared type
document.Meta!.Members["generatedAt"]    // whatever the server actually sent
```

Document meta cannot be typed on the dictionary-relationship document either:
`ResourceDocument<TAttributes, TMeta>` is indistinguishable from
`ResourceDocument<TAttributes, TRelationships>`, which C# resolves by arity alone.

## Tests as documentation

`libs/tests/JsonApiLite.Tests` (74 tests): `Serialization`/`Deserialization` pin single features
to exact wire JSON; `TypedRelationships`, `OptionalAttribute`, `SpecCompliance` cover their
namesakes; `RichDocument` → `CompoundDocument` → `RequestResponseScenario` climb from full
documents to whole client↔server cycles. Valid documents are built as objects and round-tripped
(`Wire.Roundtrip`); raw JSON appears only as expected output (wire pins) or `JsonObject`-built
protocol violations. Method names are the spec, bodies the proof.
