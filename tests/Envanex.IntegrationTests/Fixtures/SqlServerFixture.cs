using Envanex.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public TestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new TestDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM TestProducts");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
