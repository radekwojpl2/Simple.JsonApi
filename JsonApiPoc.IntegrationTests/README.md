# JsonApiPoc.IntegrationTests

End-to-end tests of the CRM API: real HTTP through the full pipeline (endpoints → MediatR → EF Core) against **PostgreSQL in a Testcontainers container**. Docker must be running; the first run pulls `postgres:16-alpine`.

## How it works

- `Infrastructure/ApiFactory` boots the app with `WebApplicationFactory<Program>`, replacing the SQLite `AppDbContext` registration with Npgsql pointing at the container. The app's own startup path (`EnsureCreated` + `Seed`) then populates the container database.
- One container and one host serve the whole run: every test class joins the `ApiCollection` xunit collection, which also serializes them. Tests that mutate data must create their own resources and delete them before finishing, so the seed dataset stays stable for the read-only tests.
- The generic JSON:API test helpers come from the `JsonApiKit.Testing` library (`libs/JsonApiKit.Testing`): `GetDocumentAsync`/`FindIdAsync` on `HttpClient`, the `ShouldMatch` subset assertion, `JsonApiMediaTypes`, and `JsonApiMember` (aliased to `Doc` via a global using in the csproj).
- `Infrastructure/` holds the app-specific constants used instead of magic strings: `Attr` (attribute names), `Rel` (relationship names), `ResourceTypes`, `Routes`, and `Seed` (the values `AppDbContext.Seed()` plants — keep it in sync with the seed method).

## Naming convention

Test classes mirror the production endpoint classes they exercise: `ContactEndpoints` → `ContactEndpointsTests`. The exception is `WorkflowScenarioTests`, which holds multi-step scenarios spanning several endpoints (pipeline progression, hypermedia navigation, pagination walks).

Test methods follow **`Operation_Scenario_Expectation`**:

| Segment | Meaning | Examples |
|---|---|---|
| Operation | The endpoint operation under test | `List`, `GetById`, `GetRelated`, `GetRelationship`, `Post`, `Patch`, `Delete`, `Lifecycle` |
| Scenario | The input or state that makes this case distinct | `Default`, `FilterByStage`, `UnknownId`, `ContactReferencedByDeal` |
| Expectation | The observable outcome | `ReturnsSeededCollection`, `Returns404Problem`, `NullsTheDealRelationship` |

So a failure like `DealEndpointsTests.Post_WrongCustomFieldType_Returns422` reads as a sentence about the API's contract without opening the file.

## Test structure

Test bodies follow **Arrange–Act–Assert**, with `// Arrange`, `// Act`, `// Assert` comments marking the phases (the Arrange comment is omitted when there is nothing to set up). Multi-step scenarios in `WorkflowScenarioTests` use narrative `// Step n — …` comments instead, because each step's assertion is the arrangement for the next.

Response shapes are asserted with `ShouldMatch` against an anonymous object rather than member-by-member `JsonNode` navigation:

```csharp
document[Doc.Data].ShouldMatch(new
{
    type = ResourceTypes.Companies,
    attributes = new { name = Seed.Companies.Globex },
    links = new { self = $"{Routes.Companies}/{id}" }
});
```

`ShouldMatch` compares objects as subsets — members the server returns beyond the expected ones are ignored — so each test states exactly the shape it cares about.
