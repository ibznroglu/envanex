using Envanex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public EnvanexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnvanexDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new EnvanexDbContext(options);
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

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
