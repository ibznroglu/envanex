using Envanex.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Envanex.Infrastructure.Identity.Configurations;

/// <summary>
/// Deliberately outside <c>Envanex.Infrastructure.Persistence.Configurations</c>: the namespace
/// predicate on <c>EnvanexDbContext.ApplyConfigurationsFromAssembly</c> is what keeps this
/// configuration — and therefore a second <c>dbo.RefreshTokens</c> — out of the business context.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RefreshTokens", AuthSchema.Name);

        // The table is append-only and inserts arrive in time order, so the cluster follows
        // CreatedAt. A clustered random-GUID primary key would fragment every insert instead.
        builder.HasKey(token => token.Id).IsClustered(false);
        builder.HasIndex(token => new { token.CreatedAt, token.Id })
            .IsClustered()
            .HasDatabaseName("IX_RefreshTokens_CreatedAt_Id");

        builder.Property(token => token.TokenHash).IsRequired().HasColumnType("binary(32)");
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Two indexes over the same single column, so both must use the named HasIndex overload:
        // the unnamed one identifies an index by its properties, and a second call would return
        // the first builder and silently rename it instead of adding an index.

        // At most one live token per family: the database-level answer to a rotation racing a
        // revocation. The filter is exactly IsUsableAt's two null checks.
        builder.HasIndex(token => token.FamilyId, "IX_RefreshTokens_FamilyId_Live")
            .IsUnique()
            .HasFilter("[RotatedAt] IS NULL AND [RevokedAt] IS NULL")
            .HasDatabaseName("IX_RefreshTokens_FamilyId_Live");

        // Unfiltered and single-column on purpose: the family queries are equality seeks on
        // FamilyId, and in an append-only table the rows a "RevokedAt IS NULL" filter would
        // exclude are the minority, so the filtered shape would be barely smaller while becoming
        // unusable for anything that must see revoked rows.
        builder.HasIndex(token => token.FamilyId, "IX_RefreshTokens_FamilyId")
            .HasDatabaseName("IX_RefreshTokens_FamilyId");

        builder.HasIndex(token => token.UserId).HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasOne<EnvanexUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Property(token => token.RevokedReason).HasMaxLength(32);

        // Explicit because AggregateRootConvention is not registered on this context. It is the
        // second half of the family lock.
        builder.Property<byte[]>(ColumnNames.RowVersion).IsRowVersion();
    }
}
