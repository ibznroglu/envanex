using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class QuantityPersistenceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public QuantityPersistenceTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<decimal> ReorderPointsAtOrRoundingToTheMaximum => new()
    {
        999999999999.999999m,
        999999999999.9999994m,
    };

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveRoundedValue()
    {
        var uom = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        var quantity = Quantity.Of(3.141593m).Value;
        var listPrice = Money.Of(10m, Currency.TRY).Value;
        var product = Product.Create("QTY-TEST", "Precision Test", uom.Id, listPrice, quantity).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.ReorderPoint.Value.ShouldBe(3.141593m);
        }
    }

    [Theory]
    [MemberData(nameof(ReorderPointsAtOrRoundingToTheMaximum))]
    public async Task SaveAndLoad_WithReorderPointAtOrRoundingToTheMaximum_ShouldStoreTheMaximum(decimal value)
    {
        var uom = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        var quantity = Quantity.Of(value).Value;
        var listPrice = Money.Of(10m, Currency.TRY).Value;
        var product = Product.Create("QTY-MAX", "Quantity Maximum", uom.Id, listPrice, quantity).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.ReorderPoint.Value.ShouldBe(999999999999.999999m);
        }
    }
}
