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
    public async Task AuthSchema_ShouldContainExactlyTheSevenExpectedIdentityTablesAndNothingElse()
    {
        // The per-table theory proves those seven live in auth; it cannot prove there are only
        // seven. An eighth table (Identity gains one, or a configuration lands in the wrong
        // context) would leave ResetIdentityAsync's seven deletes incomplete and leak state
        // between test classes silently.
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
            ],
            ignoreOrder: true,
            $"auth holds: {string.Join(", ", tables)}. Every table here must also be deleted by " +
            "SqlServerFixture.ResetIdentityAsync, directly or through a cascading foreign key.");
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
        applied.ShouldAllBe(m => !m.Contains("Identity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IdentityAppliedMigrations_ShouldNotContainBusinessMigrations()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.ShouldNotBeEmpty();
        applied.ShouldAllBe(m => m.Contains("Identity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BusinessMigrationsHistory_ShouldNotContainIdentityMigrations()
    {
        // Read the table directly rather than through EF's bookkeeping: a shared history table
        // is exactly the failure EF cannot see, because each context would read the other's rows
        // as its own.
        var migrationIds = await QueryStringsAsync("SELECT MigrationId FROM dbo.__EFMigrationsHistory");

        migrationIds.ShouldNotBeEmpty();
        migrationIds.ShouldAllBe(id => !id.Contains("Identity", StringComparison.Ordinal));
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
