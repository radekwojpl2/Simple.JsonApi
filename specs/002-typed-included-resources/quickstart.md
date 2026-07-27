# Quickstart: Typed Included Resources

**Feature**: `002-typed-included-resources` | **Date**: 2026-07-27

For an implementer picking this up, and for a consumer migrating across it.

---

## If you are migrating

Almost nothing changes. `Included`'s type moved from `IReadOnlyList<Resource>?` to `AnyIncluded?`,
but `AnyIncluded` *is* an `IReadOnlyList<Resource>`, so reading is untouched:

```csharp
document.Included![0];                                    // still fine
document.Included!.OfType<Resource<CompanyAttributes, CompanyRelationships>>();  // still fine
foreach (var resource in document.Included!) { … }        // still fine
Included = [company, tag];                                // still fine
```

**One form breaks**: assigning a collection you already have in a variable.

```csharp
// Before
var extras = BuildIncluded();
return new ResourceDocument<ContactAttributes, ContactRelationships>
{
    Data = data,
    Included = extras,          // CS0266 / CS0029
};

// After
    Included = [.. extras],     // spread
    Included = new AnyIncluded(extras),   // or construct, if the copy is unwanted
```

The compiler finds every occurrence. There is no silent behaviour change to hunt for.

---

## If you want the typed version

Three steps.

**1. Declare what your document may sideload.** One member per resource type.

```csharp
public sealed record ContactIncluded : IIncluded
{
    public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }
    public IReadOnlyList<Resource<TagAttributes, TagRelationships>>? Tags { get; init; }

    // Required by IIncluded: resources whose type no member above names.
    public IReadOnlyList<Resource> Undeclared { get; init; } = [];
}
```

You never write `"companies"` as a string — it comes from `CompanyAttributes.ResourceType`.

**2. Name it on the document.** It is the fourth type argument, so spell the meta shape too. Use
`Meta` if you have no meta shape of your own.

```csharp
ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
```

**3. Read by member.**

```csharp
var company = document.Included?.Companies?[0].Attributes?.Name;
```

No cast, no pattern match, no runtime type test. A misspelled attribute is a compile error.

---

## Gotchas

**You must spell the meta shape.** C# has no default type arguments, so declaring a sideload shape
means writing all four. `…, Meta, ContactIncluded>` is the common case. An arity-3 form meaning
"attributes, relationships, included" would collide with the existing "attributes, relationships,
meta", so this is deliberate — see [research.md](research.md) D3.

**A declared document has no untyped view.** Once you name a `TIncluded`, `document.Included` is your
record, not a list. Reaching resources your declaration did not name is `document.Included.Undeclared`
— that member exists on every implementation for exactly this reason.

**The dictionary-relationships flavour cannot declare.** `ResourceDocument<TAttributes>` keeps the
untyped form; the arity slot a typed version would need is already taken. If you need typed sideloads,
use the typed-relationships flavour.

**You no longer need a `ResourceTypeRegistry` for a declared document.** Its members already say which
types to expect, so the converter maps them itself. The registry keeps its present role for documents
that declare nothing.

---

## If you are implementing this

Build order is in [plan.md](plan.md) §Phase 2. The three things most worth knowing before you start:

1. **`AnyIncluded` implementing `IReadOnlyList<Resource>` is load-bearing, not incidental.** It is the
   single reason the break is three assignment forms instead of every call site. It needs a comment
   in the source saying so, per the house rule that comments justify decisions.

2. **Cache the type→member map per closed `TIncluded`.** Reflecting per document would be a
   performance regression on a hot path.

3. **`net8.0` is the constraining target.** `[CollectionBuilder]` and static abstract interface
   members are both available there — confirmed by compiling the probe against net8.0 — but a change
   that passes on net10.0 only is a failing change.

### Verification gates

All three must pass (constitution, *Build, Test and Verification Gates*):

```
dotnet build JsonApiLite.sln -c Release
dotnet test  JsonApiLite.sln -c Release
dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release   # not in the solution
```

Note that `JsonApiPoc.Api` consumes the **published** packages, so it will not see this change until
one is published. State that gap when reporting results rather than implying the sample was verified
against the new code.

### The probe

`scratchpad/probe/` holds the compiled evidence behind every mechanical claim in research.md — the
arity scheme, the collection-builder behaviour, the break surface and the remedies. Re-run it with
`dotnet run -c Release` if you want to check a variation before writing it into the library.
