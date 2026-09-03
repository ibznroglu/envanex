using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence;

public class EnvanexDbContext : DbContext
{
    public EnvanexDbContext(DbContextOptions<EnvanexDbContext> options) : base(options) { }

    protected EnvanexDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnvanexDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<Domain.ValueObjects.Quantity>()
            .HaveConversion<Converters.QuantityConverter>()
            .HavePrecision(18, 6);

        configurationBuilder.Properties<Domain.ValueObjects.Currency>()
            .HaveConversion<Converters.CurrencyConverter>()
            .HaveMaxLength(3);

        configurationBuilder.Conventions.Add(_ => new Conventions.AggregateRootConvention());
        configurationBuilder.Conventions.Add(_ => new Conventions.MoneyComplexTypeConvention());
    }
}
