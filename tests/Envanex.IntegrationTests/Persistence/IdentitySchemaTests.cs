using Envanex.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class IdentitySchemaTests
{
    private readonly SqlServerFixture _fixture;

    public IdentitySchemaTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IdentityMigrations_ShouldApplyToCleanDatabase()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("AspNetUsers")]
    [InlineData("AspNetRoles")]
    [InlineData("AspNetUserClaims")]
    [InlineData("AspNetUserLogins")]
    [InlineData("AspNetUserRoles")]
    [InlineData("AspNetUserTokens")]
    [InlineData("AspNetRoleClaims")]
    public async Task IdentityTables_ShouldLiveInAuthSchema(string tableName)
    {
        var schemas = await QueryStringsAsync(
            "SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @p0",
            tableName);

        schemas.ShouldBe(["auth"]);
    }

    [Fact]
    public async Task AuthSchema_ShouldContainExactlyTheEightExpectedAuthTablesAndNothingElse()
    {
        // The per-table theory proves the Identity tables live in auth; it cannot prove there are
        // only eight. A ninth table (Identity gains one, or a configuration lands in the wrong
        // context) would leave ResetIdentityAsync's deletes incomplete and leak state between test
        // classes silently.
        var tables = await QueryStringsAsync(
            """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME <> '__EFMigrationsHistory'
            ORDER BY TABLE_NAME
            """);

        // ignoreOrder: this is a set claim, and SQL Server's collation sorts AspNetUsers before
        // AspNetUserTokens while ordinal comparison does the opposite.
        tables.ShouldBe(
            [
                "AspNetRoleClaims",
                "AspNetRoles",
                "AspNetUserClaims",
                "AspNetUserLogins",
                "AspNetUserRoles",
                "AspNetUserTokens",
                "AspNetUsers",
                "RefreshTokens",
            ],
            ignoreOrder: true,
            $"auth holds: {string.Join(", ", tables)}. Every table here must also be deleted by " +
            "SqlServerFixture.ResetIdentityAsync, directly or through a cascading foreign key. " +
            "RefreshTokens is the cascade case: deleting auth.AspNetUsers takes it with it.");
    }

    [Fact]
    public async Task EmailIndex_ShouldBeUniqueFilteredAndOnNormalizedEmail()
    {
        // Identity declares EmailIndex non-unique by default, so RequireUniqueEmail would be a
        // read-then-insert check with nothing behind it. Read sys.indexes rather than trust the
        // model: the constraint that matters is the one in the database.
        //
        // is_unique alone is not enough. Losing the filter narrows the schema from "any number of
        // NULL-email users" to "at most one", and moving the index to another column removes the
        // constraint entirely -- both leave is_unique = 1. Pin the filter and the key column too,
        // or the regression surfaces much later as error 2601 far from its cause.
        var index = await QueryStringsAsync(
            """
            SELECT CONCAT(
                'unique=', CAST(i.is_unique AS varchar(1)),
                ' filtered=', CAST(i.has_filter AS varchar(1)),
                ' filter=', ISNULL(i.filter_definition, '(none)'),
                ' column=', c.name)
            FROM sys.indexes AS i
            INNER JOIN sys.tables AS t ON t.object_id = i.object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE s.name = 'auth' AND t.name = 'AspNetUsers' AND i.name = @p0
            ORDER BY ic.key_ordinal
            """,
            "EmailIndex");

        // One row, so a second key column would fail here too.
        index.ShouldBe(
            ["unique=1 filtered=1 filter=([NormalizedEmail] IS NOT NULL) column=NormalizedEmail"],
            $"auth.AspNetUsers.EmailIndex reports: {(index.Count == 0 ? "(no such index)" : string.Join(" | ", index))}. " +
            "An empty result means the index is missing or was renamed; a differing line means it is " +
            "non-unique, unfiltered, or moved to another column. Each of those drops the database " +
            "constraint behind RequireUniqueEmail.");
    }

    [Fact]
    public async Task IdentityMigrationsHistory_ShouldLiveInAuthSchema()
    {
        var schemas = await QueryStringsAsync(
            "SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @p0 ORDER BY TABLE_SCHEMA",
            "__EFMigrationsHistory");

        // Both contexts have their own history table: dbo for business, auth for Identity.
        schemas.ShouldContain("auth");
        schemas.ShouldContain("dbo");
    }

    [Fact]
    public async Task BusinessAppliedMigrations_ShouldNotContainIdentityMigrations()
    {
        await using var context = _fixture.CreateDbContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.ShouldNotBeEmpty();
        applied.ShouldAllBe(id => !IdentityMigrationIds().Contains(id));
    }

    [Fact]
    public async Task IdentityAppliedMigrations_ShouldNotContainBusinessMigrations()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.ShouldNotBeEmpty();
        applied.ShouldAllBe(id => !BusinessMigrationIds().Contains(id));
    }

    [Fact]
    public async Task BusinessMigrationsHistory_ShouldNotContainIdentityMigrations()
    {
        // Read the table directly rather than through EF's bookkeeping: a shared history table
        // is exactly the failure EF cannot see, because each context would read the other's rows
        // as its own.
        var migrationIds = await QueryStringsAsync("SELECT MigrationId FROM dbo.__EFMigrationsHistory");

        migrationIds.ShouldNotBeEmpty();
        migrationIds.ShouldAllBe(id => !IdentityMigrationIds().Contains(id));
    }

    /// <summary>
    /// The migration ids each context declares, read from the migrations assembly rather than
    /// matched on a substring of the name. A name-based rule stops guarding the moment a migration
    /// is called something that does not contain the word — <c>AddRefreshTokens</c>, for instance.
    /// </summary>
    private HashSet<string> IdentityMigrationIds()
    {
        using var context = _fixture.CreateIdentityDbContext();

        return [.. context.Database.GetMigrations()];
    }

    private HashSet<string> BusinessMigrationIds()
    {
        using var context = _fixture.CreateDbContext();

        return [.. context.Database.GetMigrations()];
    }

    private async Task<List<string>> QueryStringsAsync(string sql, string? parameter = null)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (parameter is not null)
        {
            command.Parameters.AddWithValue("@p0", parameter);
        }

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
