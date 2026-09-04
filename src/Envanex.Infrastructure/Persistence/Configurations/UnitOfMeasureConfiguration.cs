using Envanex.Domain.Aggregates.UnitOfMeasures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Envanex.Infrastructure.Persistence.Configurations;

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitOfMeasures");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Code).IsRequired().HasMaxLength(20);
        builder.HasIndex(u => u.Code).IsUnique();

        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);

        builder.Property(u => u.ConversionFactor).HasPrecision(18, 6);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(u => u.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(u => u.IsActive).IsRequired();
    }
}
