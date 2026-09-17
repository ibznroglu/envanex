using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Aggregates.Warehouses;
using Envanex.Infrastructure.Persistence.Constants;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class DbContextIsolationTests
{
    private readonly SqlServerFixture _fixture;

    public DbContextIsolationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public void EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse()
    {
        // Negative control on the namespace predicate in ApplyConfigurationsFromAssembly:
        // an over-narrow filter would silently unmap the business model instead of failing.
        using var context = _fixture.CreateDbContext();

        context.Model.FindEntityType(typeof(Product)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(UnitOfMeasure)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(Warehouse)).ShouldNotBeNull();
    }

    [Fact]
    public void EnvanexIdentityDbContext_ShouldNotMapAnyBusinessEntityType()
    {
        using var context = _fixture.CreateIdentityDbContext();

        context.Model.FindEntityType(typeof(Product)).ShouldBeNull();
        context.Model.FindEntityType(typeof(UnitOfMeasure)).ShouldBeNull();
        context.Model.FindEntityType(typeof(Warehouse)).ShouldBeNull();
    }

    [Fact]
    public void EnvanexIdentityDbContext_ShouldNotDeclareRowVersionShadowProperty()
    {
        // AggregateRootConvention is registered in EnvanexDbContext.ConfigureConventions only.
        // This proves it did not follow the Identity context.
        using var context = _fixture.CreateIdentityDbContext();

        var entityTypesWithRowVersion = context.Model
            .GetEntityTypes()
            .Where(e => e.FindProperty(ColumnNames.RowVersion) is not null)
            .Select(e => e.Name)
            .ToArray();

        entityTypesWithRowVersion.ShouldBeEmpty();
    }
}
