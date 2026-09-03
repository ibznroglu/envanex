using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Envanex.IntegrationTests.TestAggregates;
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

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveRoundedValue()
    {
        var quantity = Quantity.Of(3.141593m).Value;
        var unitPrice = Money.Of(10m, Currency.TRY).Value;
        var product = TestProduct.Create("Precision Test", unitPrice, quantity);

        await using (var context = _fixture.CreateDbContext())
        {
            context.TestProducts.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.TestProducts.SingleAsync(p => p.Id == product.Id);
            loaded.StockQuantity.Value.ShouldBe(3.141593m);
        }
    }
}
