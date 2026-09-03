using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Envanex.IntegrationTests.TestAggregates;
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

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveAmountAndCurrency()
    {
        var unitPrice = Money.Of(149.9950m, Currency.TRY).Value;
        var product = TestProduct.Create("Test Item", unitPrice, Quantity.Of(1m).Value);

        await using (var context = _fixture.CreateDbContext())
        {
            context.TestProducts.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.TestProducts.SingleAsync(p => p.Id == product.Id);
            loaded.UnitPrice.Amount.ShouldBe(149.9950m);
            loaded.UnitPrice.Currency.Code.ShouldBe("TRY");
        }
    }
}
