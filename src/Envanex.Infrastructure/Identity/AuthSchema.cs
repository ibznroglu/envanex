namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The single source of truth for where the Identity model lives in the database.
/// Both the schema name and the migrations history table are read from here so that
/// the two contexts can never end up sharing one applied-migrations table.
/// </summary>
internal static class AuthSchema
{
    public const string Name = "auth";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
}
