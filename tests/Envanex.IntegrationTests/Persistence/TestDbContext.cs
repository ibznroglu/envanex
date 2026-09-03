using Envanex.Infrastructure.Persistence;
using Envanex.IntegrationTests.TestAggregates;
using Microsoft.EntityFrameworkCore;

namespace Envanex.IntegrationTests.Persistence;

public sealed class TestDbContext : EnvanexDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<TestProduct> TestProducts => Set<TestProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TestProductConfiguration());
    }
}
