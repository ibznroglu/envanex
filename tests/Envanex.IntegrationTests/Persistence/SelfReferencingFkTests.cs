using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class SelfReferencingFkTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public SelfReferencingFkTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DerivedUnit_WithValidBaseUnit_ShouldPersist()
    {
        var baseUnit = UnitOfMeasure.Create("KG", "Kilogram", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(baseUnit);
            await context.SaveChangesAsync();
        }

        var derivedUnit = UnitOfMeasure.Create("GR", "Gram", baseUnit.Id, 0.001m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(derivedUnit);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.UnitOfMeasures.SingleAsync(u => u.Id == derivedUnit.Id);
            loaded.BaseUnitId.ShouldBe(baseUnit.Id);
            loaded.ConversionFactor.ShouldBe(0.001m);
        }
    }

    [Fact]
    public async Task DerivedUnit_WithNonExistentBaseUnit_ShouldThrowDbUpdateException()
    {
        var nonExistentId = Guid.CreateVersion7();
        var derivedUnit = UnitOfMeasure.Create("GR", "Gram", nonExistentId, 0.001m).Value;

        await using var context = _fixture.CreateDbContext();
        context.UnitOfMeasures.Add(derivedUnit);

        await Should.ThrowAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }
}
