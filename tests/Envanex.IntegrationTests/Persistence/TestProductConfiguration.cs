using Envanex.IntegrationTests.TestAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Envanex.IntegrationTests.Persistence;

internal sealed class TestProductConfiguration : IEntityTypeConfiguration<TestProduct>
{
    public void Configure(EntityTypeBuilder<TestProduct> builder)
    {
        builder.ToTable("TestProducts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.ComplexProperty(p => p.UnitPrice);
    }
}
