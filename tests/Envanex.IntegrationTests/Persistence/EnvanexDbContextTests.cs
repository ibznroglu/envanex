using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;
using Envanex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

public class EnvanexDbContextTests
{
    [Fact]
    public void Model_BuildsSuccessfully()
    {
        using var context = CreateTestContext();

        var model = context.Model;

        model.ShouldNotBeNull();
    }

    [Fact]
    public void AggregateRootConvention_AddsRowVersionShadowProperty()
    {
        using var context = CreateTestContext();
        var entityType = context.Model.FindEntityType(typeof(TestAggregate));

        entityType.ShouldNotBeNull();

        var rowVersion = entityType.FindProperty("RowVersion");
        rowVersion.ShouldNotBeNull();
        rowVersion.ClrType.ShouldBe(typeof(byte[]));
        rowVersion.IsConcurrencyToken.ShouldBeTrue();
        rowVersion.ValueGenerated.ShouldBe(ValueGenerated.OnAddOrUpdate);
        rowVersion.IsShadowProperty().ShouldBeTrue();
    }

    [Fact]
    public void AggregateRootConvention_SkipsNonAggregateRootEntities()
    {
        using var context = CreateTestContext();
        var entityType = context.Model.FindEntityType(typeof(TestNonAggregate));

        entityType.ShouldNotBeNull();

        var rowVersion = entityType.FindProperty("RowVersion");
        rowVersion.ShouldBeNull();
    }

    [Fact]
    public void QuantityProperty_UsesConverterWithCorrectPrecision()
    {
        using var context = CreateTestContext();
        var entityType = context.Model.FindEntityType(typeof(TestAggregate));
        entityType.ShouldNotBeNull();

        var quantityProp = entityType.FindProperty(nameof(TestAggregate.Stock));
        quantityProp.ShouldNotBeNull();
        quantityProp.GetValueConverter().ShouldNotBeNull();
        quantityProp.GetPrecision().ShouldBe(18);
        quantityProp.GetScale().ShouldBe(6);
    }

    [Fact]
    public void CurrencyProperty_UsesConverterWithMaxLength()
    {
        using var context = CreateTestContext();
        var entityType = context.Model.FindEntityType(typeof(TestAggregate));
        entityType.ShouldNotBeNull();

        var currencyProp = entityType.FindProperty(nameof(TestAggregate.DefaultCurrency));
        currencyProp.ShouldNotBeNull();
        currencyProp.GetValueConverter().ShouldNotBeNull();
        currencyProp.GetMaxLength().ShouldBe(3);
    }

    private static TestDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<EnvanexDbContext>()
            .UseSqlServer("Server=fake;Database=fake")
            .Options;

        return new TestDbContext(options);
    }

    /// <summary>
    /// A test-only aggregate root to verify that conventions are applied correctly.
    /// </summary>
    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public Quantity Stock { get; private set; }
        public Currency DefaultCurrency { get; private set; } = null!;

        private TestAggregate() { }
    }

    /// <summary>
    /// A plain entity (not an aggregate root) to verify that RowVersion is NOT applied.
    /// </summary>
    private sealed class TestNonAggregate : Entity<Guid>
    {
        public string Name { get; private set; } = string.Empty;

        private TestNonAggregate() { }
    }

    /// <summary>
    /// A DbContext subclass that registers test entities so conventions can be verified.
    /// Inherits all convention and converter configuration from EnvanexDbContext.
    /// </summary>
    private sealed class TestDbContext : EnvanexDbContext
    {
        public TestDbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(e => e.Id);
            });

            modelBuilder.Entity<TestNonAggregate>(builder =>
            {
                builder.HasKey(e => e.Id);
            });
        }
    }
}
