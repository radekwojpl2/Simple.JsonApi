# JsonApiKit.OpenApi

OpenAPI integration for [JsonApiKit](../JsonApiKit/README.md). Optional — the core library works without it.

`JsonApiQuery` binds through `BindAsync`, so the JSON:API query parameters it handles are invisible to ASP.NET Core's OpenAPI generation. This package adds an operation transformer that documents them from each endpoint's `WithJsonApiQuery(...)` metadata — the same metadata that drives validation, so the docs can't drift from the behavior.

## Usage

```csharp
using JsonApiKit.OpenApi;

builder.Services.AddOpenApi(o => o
    .AddJsonApiQueryParameters()
    .AddJsonApiLinkageBodies()
    .AddJsonApiResourceDocumentBodies());
```

Every endpoint carrying `JsonApiQueryOptions` metadata gains query parameters in the generated document:

- `include` and `sort`, listing the allowed values
- `page[number]` and `page[size]` (integers), unless the endpoint declared `paging: false`
- `filter[name]` per declared filter, listing the allowed values when constrained
- `fields[type]` per type in `fieldsFor`, listing the map's declared fields when a `ResourceMapRegistry` is registered

`AddJsonApiLinkageBodies` documents `/relationships/{name}` PATCH bodies declared via `WithToOneLinkageBody` with a linkage description and a typed example.

`AddJsonApiResourceDocumentBodies` documents write bodies declared via `WithResourceDocumentBody`. Those endpoints bind `JsonNode` (the document needs structural validation before typing), which would otherwise surface as an untyped schema; the transformer replaces it with a full JSON:API resource-document schema — `data.type` (and `data.id` on updates), the attributes record, and per-relationship linkage marking required and clearable relationships.

Endpoints without the metadata are left untouched.
