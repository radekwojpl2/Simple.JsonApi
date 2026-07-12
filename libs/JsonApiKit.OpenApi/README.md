# JsonApiKit.OpenApi

OpenAPI integration for [JsonApiKit](../JsonApiKit/README.md). Optional — the core library works without it.

`JsonApiQuery` binds through `BindAsync`, so the JSON:API query parameters it handles are invisible to ASP.NET Core's OpenAPI generation. This package adds an operation transformer that documents them from each endpoint's `WithJsonApiQuery(...)` metadata — the same metadata that drives validation, so the docs can't drift from the behavior.

## Usage

```csharp
using JsonApiKit.OpenApi;

builder.Services.AddOpenApi(o => o.AddJsonApiQueryParameters());
```

Every endpoint carrying `JsonApiQueryOptions` metadata gains query parameters in the generated document:

- `include` and `sort`, listing the allowed values
- `page[number]` and `page[size]` (integers), unless the endpoint declared `paging: false`
- `filter[name]` per declared filter, listing the allowed values when constrained
- `fields[type]` per type in `fieldsFor`, listing the map's declared fields when a `ResourceMapRegistry` is registered

Endpoints without the metadata are left untouched.
