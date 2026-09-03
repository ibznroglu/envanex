using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Envanex.IntegrationTests.TestAggregates;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class ConcurrencyTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public ConcurrencyTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        var unitPrice = Money.Of(100m, Currency.TRY).Value;
        var product = TestProduct.Create("Concurrency Test", unitPrice, Quantity.Of(1m).Value);

        await using (var context = _fixture.CreateDbContext())
        {
            context.TestProducts.Add(product);
            await context.SaveChangesAsync();
        }

        // Load same product in two separate contexts
        await using var context1 = _fixture.CreateDbContext();
        await using var context2 = _fixture.CreateDbContext();

        var product1 = await context1.TestProducts.SingleAsync(p => p.Id == product.Id);
        var product2 = await context2.TestProducts.SingleAsync(p => p.Id == product.Id);

        // Update and save in context1
        product1.UpdatePrice(Money.Of(200m, Currency.TRY).Value);
        await context1.SaveChangesAsync();

        // Update and try to save in context2 -- should fail
        product2.UpdatePrice(Money.Of(300m, Currency.TRY).Value);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => context2.SaveChangesAsync());
    }
}
