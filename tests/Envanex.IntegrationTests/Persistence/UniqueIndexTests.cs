using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.ValueObjects;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class UniqueIndexTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public UniqueIndexTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InsertDuplicateProductCode_ShouldThrowDbUpdateException()
    {
        var uom = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        var listPrice = Money.Of(10m, Currency.TRY).Value;
        var product1 = Product.Create("DUPLICATE", "First Product", uom.Id, listPrice, Quantity.Of(1m).Value).Value;
        var product2 = Product.Create("DUPLICATE", "Second Product", uom.Id, listPrice, Quantity.Of(1m).Value).Value;

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product1);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateDbContext())
        {
            context.Products.Add(product2);
            await Should.ThrowAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }
    }
}
