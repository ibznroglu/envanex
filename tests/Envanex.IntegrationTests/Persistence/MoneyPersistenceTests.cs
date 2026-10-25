using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class MoneyPersistenceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public MoneyPersistenceTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<decimal> AmountsAtOrRoundingToTheMaximum => new()
    {
        99999999999999.9999m,
        99999999999999.99994m,
    };

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveAmountAndCurrency()
    {
        var uom = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        var listPrice = Money.Of(149.9950m, Currency.TRY).Value;
        var product = Product.Create("MONEY-TEST", "Money Test Item", uom.Id, listPrice, Quantity.Of(1m).Value).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.ListPrice.Amount.ShouldBe(149.9950m);
            loaded.ListPrice.Currency.Code.ShouldBe("TRY");
        }
    }

    [Theory]
    [MemberData(nameof(AmountsAtOrRoundingToTheMaximum))]
    public async Task SaveAndLoad_WithAmountAtOrRoundingToTheMaximum_ShouldStoreTheMaximum(decimal amount)
    {
        var uom = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        var listPrice = Money.Of(amount, Currency.TRY).Value;
        var product = Product.Create("MONEY-MAX", "Money Maximum Item", uom.Id, listPrice, Quantity.Of(1m).Value).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.ListPrice.Amount.ShouldBe(99999999999999.9999m);
        }
    }
}
