using Envanex.Domain.Aggregates.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Envanex.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Code).IsRequired().HasMaxLength(20);
        builder.HasIndex(w => w.Code).IsUnique();

        builder.Property(w => w.Name).IsRequired().HasMaxLength(200);

        builder.Property(w => w.IsActive).IsRequired();
    }
}
