using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class ConversionFactorPersistenceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public ConversionFactorPersistenceTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<decimal> BoundaryFactors => new()
    {
        0.000001m,
        999999999999.999999m,
    };

    [Theory]
    [MemberData(nameof(BoundaryFactors))]
    public async Task DerivedUnit_WithBoundaryFactor_ShouldRoundTripExactly(decimal factor)
    {
        decimal loadedFactor = await SaveDerivedUnitAndReadFactorAsync(factor);

        loadedFactor.ShouldBe(factor);
    }

    [Fact]
    public async Task DerivedUnit_WithFactorWithTrailingZeros_ShouldRoundTripByValue()
    {
        decimal loadedFactor = await SaveDerivedUnitAndReadFactorAsync(1.500000000m);

        loadedFactor.ShouldBe(1.5m);
    }

    private async Task<decimal> SaveDerivedUnitAndReadFactorAsync(decimal factor)
    {
        var baseUnit = UnitOfMeasure.Create("KG", "Kilogram", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(baseUnit);
            await context.SaveChangesAsync();
        }

        var derivedUnit = UnitOfMeasure.Create("GR", "Gram", baseUnit.Id, factor).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(derivedUnit);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.UnitOfMeasures.SingleAsync(u => u.Id == derivedUnit.Id);
            return loaded.ConversionFactor;
        }
    }
}
