using System.Reflection;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Aggregates.Warehouses;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class DbContextIsolationTests
{
    private readonly SqlServerFixture _fixture;

    public DbContextIsolationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public void EnvanexDbContext_ShouldStillApplyProductUnitOfMeasureAndWarehouseConfigurations()
    {
        // Negative control on the namespace predicate in ApplyConfigurationsFromAssembly.
        // Asserting that the three entity types are found would prove nothing about the
        // predicate: EF discovers them from the DbSet<T> properties whatever the predicate
        // returns. The max lengths and the unique index below come from the three
        // IEntityTypeConfiguration classes and from nowhere else, so an over-narrow filter
        // takes them with it.
        using var context = _fixture.CreateDbContext();

        context.Model.FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.Code))!.GetMaxLength().ShouldBe(50);
        context.Model.FindEntityType(typeof(UnitOfMeasure))!
            .FindProperty(nameof(UnitOfMeasure.Code))!.GetMaxLength().ShouldBe(20);
        context.Model.FindEntityType(typeof(Warehouse))!
            .FindProperty(nameof(Warehouse.Code))!.GetMaxLength().ShouldBe(20);

        var warehouseCodeIndex = context.Model.FindEntityType(typeof(Warehouse))!
            .GetIndexes()
            .SingleOrDefault(index =>
                index.Properties.Count == 1 && index.Properties[0].Name == nameof(Warehouse.Code));

        warehouseCodeIndex.ShouldNotBeNull();
        warehouseCodeIndex.IsUnique.ShouldBeTrue();
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
    public void EnvanexDbContext_ShouldNotMapRefreshToken()
    {
        // The guard the namespace predicate exists for. RefreshTokenConfiguration is an
        // IEntityTypeConfiguration<RefreshToken> in the same assembly, and internal configurations
        // are demonstrably discovered -- ProductConfiguration is internal sealed and Products is
        // mapped -- so deleting the predicate creates a second dbo.RefreshTokens and turns this red.
        using var context = _fixture.CreateDbContext();

        context.Model.FindEntityType(typeof(RefreshToken)).ShouldBeNull();
    }

    [Fact]
    public void EnvanexIdentityDbContext_ShouldMapRefreshToken()
    {
        // Positive control: without it, a mis-namespaced or unregistered configuration that maps
        // the entity into no context at all would pass the test above.
        using var context = _fixture.CreateIdentityDbContext();

        var entityType = context.Model.FindEntityType(typeof(RefreshToken));

        entityType.ShouldNotBeNull();
        entityType.GetSchema().ShouldBe("auth");
    }

    [Fact]
    public void EnvanexIdentityDbContext_ShouldNotOverrideConfigureConventions()
    {
        // AggregateRootConvention and MoneyComplexTypeConvention are registered in
        // EnvanexDbContext.ConfigureConventions, and the Identity context stays clear of them by
        // not overriding that method at all. Asserting instead that no Identity entity type
        // carries a RowVersion shadow property could not regress: AggregateRootConvention only
        // acts on types inheriting AggregateRoot<>, and no Identity entity does. This is the
        // assertion the separation actually rests on.
        var configureConventions = typeof(EnvanexIdentityDbContext).GetMethod(
            "ConfigureConventions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        configureConventions.ShouldNotBeNull();
        configureConventions.DeclaringType.ShouldNotBe(typeof(EnvanexIdentityDbContext));
    }
}
