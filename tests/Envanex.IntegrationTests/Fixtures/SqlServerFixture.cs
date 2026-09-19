using Envanex.Infrastructure.Identity;
using Envanex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public EnvanexWebApplicationFactory WebApplicationFactory { get; private set; } = null!;

    public EnvanexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnvanexDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new EnvanexDbContext(options);
    }

    public EnvanexIdentityDbContext CreateIdentityDbContext()
    {
        // Two steps on purpose: UseEnvanexIdentitySqlServer returns the non-generic builder,
        // whose Options property is not DbContextOptions<EnvanexIdentityDbContext>.
        var optionsBuilder = new DbContextOptionsBuilder<EnvanexIdentityDbContext>();
        optionsBuilder.UseEnvanexIdentitySqlServer(ConnectionString);

        return new EnvanexIdentityDbContext(optionsBuilder.Options);
    }

    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();

        // Deletes follow FK dependency order: children before parents.
        // Update this list when new tables are added.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM UnitOfMeasures WHERE BaseUnitId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM UnitOfMeasures WHERE BaseUnitId IS NULL");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Warehouses");
    }

    /// <summary>
    /// Resets the Identity context only. Deliberately separate from <see cref="ResetAsync"/>:
    /// the auth tables are a different context with a different FK graph, and mixing the two
    /// lists is what makes a hand-maintained delete order rot.
    /// </summary>
    public async Task ResetIdentityAsync()
    {
        await using var context = CreateIdentityDbContext();

        // Deletes follow FK dependency order: children before parents.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserTokens");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserLogins");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserClaims");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserRoles");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetRoleClaims");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetRoles");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUsers");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        await using var identityContext = CreateIdentityDbContext();
        await identityContext.Database.MigrateAsync();

        WebApplicationFactory = new EnvanexWebApplicationFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (WebApplicationFactory is not null)
        {
            await WebApplicationFactory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
