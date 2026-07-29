# Public API Contract: Typed Included Resources

**Feature**: `002-typed-included-resources` | **Date**: 2026-07-27

This library's external interface is its public C# surface plus the JSON it reads and writes. Both
are contracts; this file records what each becomes. Signatures below were compiled by the probe in
[../research.md](../research.md).

---

## 1. The JSON contract — unchanged

The wire format does not move. This is FR-010 and FR-015, and it is the property that makes the
change safe despite being source-breaking.

```jsonc
{
  "data": { "type": "contacts", "id": "1", "attributes": { "name": "Ada" } },
  "included": [
    { "type": "companies", "id": "7", "attributes": { "name": "Acme" } },
    { "type": "tags",      "id": "1", "attributes": { "label": "vip" } }
  ]
}
```

One flat array holding several types, as the specification requires. Checked
https://jsonapi.org/format/ (*Compound Documents*):

> "In a compound document, all included resources **MUST** be represented as an array of resource
> objects in a top-level `included` member."

A declared document and an undeclared one produce identical bytes for identical content.

---

## 2. New public types

```csharp
/// <summary>Marks a record as a document's sideload shape — one member per resource type the
/// document may sideload, each an IReadOnlyList of that resource. Sits over the single flat array
/// the specification requires, as IRelationships sits over a name-keyed object.</summary>
public interface IIncluded;   // empty marker, like IAttributes and IRelationships

/// <summary>The sideload shape for a document that declares no sideloadable types: the resources
/// as they arrived, untyped. Implements IReadOnlyList&lt;Resource&gt; so that reading a document
/// which declares nothing is unchanged from before this type existed.</summary>
[CollectionBuilder(typeof(AnyIncludedBuilder), nameof(AnyIncludedBuilder.Create))]
public sealed record AnyIncluded : IIncluded, IReadOnlyList<Resource>
{
    public AnyIncluded(IReadOnlyList<Resource> resources);

    public Resource this[int index] { get; }
    public int Count { get; }
    public IEnumerator<Resource> GetEnumerator();
}

/// <summary>Builds an AnyIncluded from a collection expression, so `Included = [a, b]` keeps
/// working. Referenced only by the CollectionBuilder attribute.</summary>
public static class AnyIncludedBuilder
{
    public static AnyIncluded Create(ReadOnlySpan<Resource> items);
}
```

---

## 3. Changed public types

```csharp
// New arity-4 form. TIncluded is appended, never inserted, so arities 1-3 keep their meanings.
public record ResourceDocument<TAttributes, TRelationships, TMeta, TIncluded>
    where TAttributes   : class, IAttributes
    where TRelationships: class, IRelationships
    where TMeta         : class, IMeta
    where TIncluded     : class, IIncluded
{
    public required Resource<TAttributes, TRelationships>? Data { get; init; }
    public TIncluded? Included { get; init; }   // was IReadOnlyList<Resource>?
    public Links? Links { get; init; }
    public TMeta? Meta { get; init; }
    public JsonApiObject? JsonApi { get; init; }
}

// Arity 3 — third parameter is STILL TMeta. Defaults TIncluded to AnyIncluded.
public record ResourceDocument<TAttributes, TRelationships, TMeta>
    : ResourceDocument<TAttributes, TRelationships, TMeta, AnyIncluded>;

// Arity 2 — unchanged meaning.
public sealed record ResourceDocument<TAttributes, TRelationships>
    : ResourceDocument<TAttributes, TRelationships, Meta>;
```

`ResourceCollectionDocument` mirrors this. `ToOneLinkageDocument`, `ToManyLinkageDocument` and
`ErrorDocument` are **unchanged** — no sideload member, per FR-020.

---

## 4. The break, exactly

Established by compiling each form in isolation (research D5). This is the complete list.

### Still compiles — no edit

| Form | Example |
| --- | --- |
| Collection-expression literal | `Included = [company, tag]` |
| Null | `Included = null` |
| Indexing | `document.Included![0]` |
| `OfType<T>()` | `document.Included!.OfType<Resource<CompanyAttributes, …>>()` |
| `foreach` | `foreach (var r in document.Included!)` |
| Assignment *to* a list variable | `IReadOnlyList<Resource> x = document.Included;` |

### Breaks — one mechanical edit

Assigning a pre-existing collection *variable* to `Included`:

| Case | Compiler error |
| --- | --- |
| `IReadOnlyList<Resource>` variable | `CS0266: Cannot implicitly convert type 'IReadOnlyList<Resource>' to 'AnyIncluded'` |
| `Resource[]` variable | `CS0029: Cannot implicitly convert type 'Resource[]' to 'AnyIncluded'` |
| LINQ result | `CS0029: Cannot implicitly convert type 'List<Resource<…>>' to 'AnyIncluded'` |

**Every break is a compile error. None is a silent behaviour change** — the property that makes this
safe to ship.

### The migration note (FR-022)

```csharp
// Before
Included = resources,

// After — either
Included = [.. resources],
// or, where the spread is unwanted
Included = new AnyIncluded(resources),
```

Both forms were compiled and run; each produced the expected element count.

---

## 5. Declaring and reading — the feature in use

```csharp
// Declare once, next to the document.
public sealed record ContactIncluded : IIncluded
{
    public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }
    public IReadOnlyList<Resource<TagAttributes, TagRelationships>>? Tags { get; init; }
}

// Read with no cast and no pattern match — this is the whole point (FR-004).
var document = JsonApiSerializer
    .Deserialize<ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>>(json);

string? company = document?.Included?.Companies?[0].Attributes?.Name;
```

The wire type name is never written as a string: it comes from `CompanyAttributes.ResourceType` via
`IResourceType`, so a typo is a compile error (FR-002, FR-005).

---

## 6. What a declared document does *not* offer

Per FR-017, a declared document exposes the typed view only. This is enforced by the compiler, not by
convention — attempting the untyped read produces:

```
error CS8121: An expression of type 'ContactIncluded' cannot be handled by a pattern of
type 'IReadOnlyList<Resource>'
```

Encountered while writing the probe, and retained as the evidence for FR-017. An author cannot
accidentally fall back to the untyped view on a declared document, because such code does not build.
