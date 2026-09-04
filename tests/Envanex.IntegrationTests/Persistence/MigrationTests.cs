using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class MigrationTests
{
    private readonly SqlServerFixture _fixture;

    public MigrationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_ShouldApplyToCleanDatabase()
    {
        await using var context = _fixture.CreateDbContext();
        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.ShouldBeEmpty();
    }
}
