using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaCreationTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public SchemaCreationTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Database_ShouldBeConnectableAndTableShouldExist()
    {
        await using var context = _fixture.CreateDbContext();

        var canConnect = await context.Database.CanConnectAsync();
        canConnect.ShouldBeTrue();

        // Verify TestProducts table exists by querying it
        var count = await context.TestProducts.CountAsync();
        count.ShouldBe(0);
    }
}
