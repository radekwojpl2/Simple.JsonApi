using JsonApiPoc.Application.Data;

namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>Base class for endpoint tests: empties the database before each test, so every test
/// arranges exactly the rows its assertions depend on via <see cref="ArrangeAsync{T}"/>. xUnit
/// builds a fresh test-class instance per test method, so InitializeAsync runs per test — mutating
/// tests need no cleanup and cannot leak state into their neighbours.</summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected readonly HttpClient Client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Plants this test's rows into the empty database. Returns the value
    /// <paramref name="arrange"/> produces after SaveChanges, so tests can keep the entities they
    /// created and read their database-generated ids.</summary>
    protected Task<T> ArrangeAsync<T>(Func<AppDbContext, T> arrange) => factory.ArrangeAsync(arrange);

    /// <inheritdoc cref="ArrangeAsync{T}"/>
    protected Task ArrangeAsync(Action<AppDbContext> arrange) =>
        factory.ArrangeAsync(db =>
        {
            arrange(db);
            return true;
        });
}
