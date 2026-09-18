using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The one place that knows how to point a <see cref="DbContextOptionsBuilder"/> at the Identity
/// database, so the <c>auth</c> migrations history table cannot be configured in one of the three
/// call sites (DI, the design-time factory, the test fixture) and forgotten in the others.
/// </summary>
internal static class IdentityDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseEnvanexIdentitySqlServer(
        this DbContextOptionsBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionString);

        // Without the explicit history table both contexts read dbo.__EFMigrationsHistory as their
        // own, and the failure is silent until a migration is skipped or re-applied.
        return builder.UseSqlServer(
            connectionString,
            sqlServer => sqlServer.MigrationsHistoryTable(
                AuthSchema.MigrationsHistoryTable,
                AuthSchema.Name));
    }
}
