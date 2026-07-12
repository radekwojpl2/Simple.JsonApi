using JsonApiPoc.Application.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>Boots the real application against a PostgreSQL Testcontainers instance instead of
/// SQLite. The app's startup seed is wiped before every test — tests arrange the rows they need
/// via <see cref="ArrangeAsync"/> and exercise them over real HTTP.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    static ApiFactory() =>
        // The app assigns mixed DateTime kinds (seed is UTC, request bodies parse as Unspecified);
        // legacy mode maps them all to 'timestamp' so Npgsql doesn't reject non-UTC values.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        });

    public Task InitializeAsync() => _postgres.StartAsync();

    /// <summary>Empties every table, so each test starts from a blank database regardless of what
    /// the app's startup seed or the previous test wrote. RESTART IDENTITY makes generated ids
    /// repeatable too.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .Distinct()
            .Select(table => $"\"{table}\"");
        // Table names come from the EF model, not from request data, so there is nothing to parameterize.
        var truncate = "TRUNCATE TABLE " + string.Join(", ", tables) + " RESTART IDENTITY CASCADE";
        await db.Database.ExecuteSqlRawAsync(truncate);
    }

    /// <summary>Plants the rows <paramref name="arrange"/> adds and saves them. Returns
    /// <paramref name="arrange"/>'s result after SaveChanges, so a test can hand back the entities
    /// it created and read their database-generated ids.</summary>
    public async Task<T> ArrangeAsync<T>(Func<AppDbContext, T> arrange)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var arranged = arrange(db);
        await db.SaveChangesAsync();
        return arranged;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
