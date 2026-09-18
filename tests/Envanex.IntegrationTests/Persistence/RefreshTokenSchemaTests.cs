using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenSchemaTests : IAsyncLifetime
{
    private static readonly TimeSpan IdleWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan AbsoluteWindow = TimeSpan.FromDays(30);
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private Guid _userId;

    public RefreshTokenSchemaTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        // Refresh token rows need a user to point at. The email is unique per call because
        // auth.AspNetUsers.EmailIndex is a unique filtered index and this seeder writes
        // NormalizedEmail directly, without UserValidator in front of it.
        _userId = await IdentityRowSeeder.InsertBareUserRowAsync(
            _fixture,
            $"refresh-token-schema-{Guid.NewGuid():N}@envanex.test");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RefreshTokens_ShouldLiveInAuthSchema()
    {
        var schemas = await QueryStringsAsync(
            "SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RefreshTokens'");

        schemas.ShouldBe(["auth"]);
    }

    [Fact]
    public async Task RefreshTokens_TokenHash_ShouldBeBinary32AndNotNullable()
    {
        // Fixed-width binary, not varbinary and not a string: the column stores exactly one
        // SHA-256 digest, and a nullable or variable-width column would let a row that matches
        // nothing be written.
        var column = await QueryStringsAsync(
            """
            SELECT CONCAT(DATA_TYPE, '(', CHARACTER_MAXIMUM_LENGTH, ') nullable=', IS_NULLABLE)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'RefreshTokens' AND COLUMN_NAME = 'TokenHash'
            """);

        column.ShouldBe(["binary(32) nullable=NO"]);
    }

    [Fact]
    public async Task RefreshTokens_TokenHashIndex_ShouldBeUnique()
    {
        var index = await QueryIndexAsync("IX_RefreshTokens_TokenHash");

        index.ShouldBe(["unique=1 filtered=0 filter=(none) column=TokenHash"]);
    }

    [Fact]
    public async Task RefreshTokens_FamilyLiveIndex_ShouldBeUniqueAndFiltered()
    {
        // This index is half of the family lock. Losing IsUnique turns "at most one live token per
        // family" into a comment, and losing the filter makes every rotation a violation.
        var index = await QueryIndexAsync("IX_RefreshTokens_FamilyId_Live");

        index.Count.ShouldBe(1, "IX_RefreshTokens_FamilyId_Live is missing, renamed, or has a second key column.");
        index[0].ShouldStartWith("unique=1 filtered=1 ");
        index[0].ShouldEndWith("column=FamilyId");
        index[0].ShouldContain("RotatedAt");
        index[0].ShouldContain("RevokedAt");
    }

    [Fact]
    public async Task RefreshTokens_FamilyIdIndex_ShouldExistAndBeUnfiltered()
    {
        // The three family queries must see revoked and rotated rows, so the seek index behind
        // them cannot be the filtered one above. Two indexes over one column need the named
        // HasIndex overload; the unnamed one would rename the first instead of adding a second.
        var index = await QueryIndexAsync("IX_RefreshTokens_FamilyId");

        index.ShouldBe(["unique=0 filtered=0 filter=(none) column=FamilyId"]);
    }

    [Fact]
    public async Task RefreshTokens_PrimaryKey_ShouldBeNonClustered()
    {
        var primaryKey = await QueryStringsAsync(
            """
            SELECT i.type_desc
            FROM sys.indexes AS i
            INNER JOIN sys.tables AS t ON t.object_id = i.object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = 'auth' AND t.name = 'RefreshTokens' AND i.is_primary_key = 1
            """);

        primaryKey.ShouldBe(["NONCLUSTERED"]);
    }

    [Fact]
    public async Task RefreshTokens_ClusteredIndex_ShouldBeOnCreatedAtAndId()
    {
        // Append-only inserts arrive in time order. Clustering on a random GUID primary key
        // instead would fragment the table on every insert.
        var keyColumns = await QueryStringsAsync(
            """
            SELECT c.name
            FROM sys.indexes AS i
            INNER JOIN sys.tables AS t ON t.object_id = i.object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE s.name = 'auth' AND t.name = 'RefreshTokens' AND i.type_desc = 'CLUSTERED'
            ORDER BY ic.key_ordinal
            """);

        keyColumns.ShouldBe(["CreatedAt", "Id"]);
    }

    [Fact]
    public async Task RefreshTokens_UserId_ShouldHaveCascadingForeignKeyToAspNetUsers()
    {
        // Cascade is what lets ResetIdentityAsync stay at seven deletes: removing the users
        // removes their tokens.
        var foreignKeys = await QueryStringsAsync(
            """
            -- COLLATE DATABASE_DEFAULT: the sys catalog name columns and the action description
            -- carry different collations, and concatenating them raw is a hard error.
            SELECT CONCAT(
                rs.name COLLATE DATABASE_DEFAULT, '.',
                rt.name COLLATE DATABASE_DEFAULT,
                ' ondelete=', fk.delete_referential_action_desc)
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
            INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
            WHERE s.name = 'auth' AND t.name = 'RefreshTokens'
            """);

        foreignKeys.ShouldBe(["auth.AspNetUsers ondelete=CASCADE"]);
    }

    [Fact]
    public async Task RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation()
    {
        // The test that protects IX_RefreshTokens_FamilyId_Live. Deterministic, no race: the
        // parent is deliberately left unrotated, so both rows match the filter at once.
        var parent = NewRoot();

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(parent);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(NewChild(parent, Now.AddMinutes(1)));

            var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

            UniqueViolationNumberOf(exception).ShouldBeOneOf(2601, 2627);
        }
    }

    [Fact]
    public async Task RefreshTokens_InsertingASecondTokenWithTheSameHash_ShouldThrowUniqueViolation()
    {
        var hash = RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken());

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(
                RefreshToken.CreateRoot(_userId, hash, Now, IdleWindow, AbsoluteWindow));
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            // A different family, so only the hash collides.
            context.RefreshTokens.Add(
                RefreshToken.CreateRoot(_userId, hash, Now, IdleWindow, AbsoluteWindow));

            var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

            UniqueViolationNumberOf(exception).ShouldBeOneOf(2601, 2627);
        }
    }

    [Fact]
    public async Task RefreshTokens_RotatedRowAndItsChild_ShouldCoexistInTheSameFamily()
    {
        // The direct regression for "deleting the predecessor destroys reuse detection after one
        // generation". Both rows must survive, so that replaying r1 still resolves to the family
        // r3 is live in.
        var parent = NewRoot();

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(parent);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            var stored = await context.RefreshTokens.SingleAsync(token => token.Id == parent.Id);
            var child = NewChild(stored, Now.AddMinutes(1));
            stored.MarkRotated(Now.AddMinutes(1), child.Id);
            context.RefreshTokens.Add(child);

            // One SaveChangesAsync, exactly as rotation will do it: the consumed row is stamped
            // and the successor inserted in the same save.
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            var family = await context.RefreshTokens
                .Where(token => token.FamilyId == parent.FamilyId)
                .OrderBy(token => token.CreatedAt)
                .ToListAsync();

            family.Count.ShouldBe(2);
            family[0].Id.ShouldBe(parent.Id);
            family[0].RotatedAt.ShouldNotBeNull();
            family[0].ReplacedByTokenId.ShouldBe(family[1].Id);
            family[1].RotatedAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task RefreshTokens_UpdatingARowLoadedBeforeAConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        // The guard for the other half of the family lock: the RowVersion token. Deterministic,
        // no race -- the two contexts are simply interleaved by hand.
        var token = NewRoot();

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(token);
            await context.SaveChangesAsync();
        }

        await using var stale = _fixture.CreateIdentityDbContext();
        var loadedFirst = await stale.RefreshTokens.SingleAsync(t => t.Id == token.Id);

        await using (var winner = _fixture.CreateIdentityDbContext())
        {
            var loadedSecond = await winner.RefreshTokens.SingleAsync(t => t.Id == token.Id);
            loadedSecond.Revoke(Now.AddMinutes(1), RefreshTokenRevocationReason.Logout);
            await winner.SaveChangesAsync();
        }

        loadedFirst.MarkRotated(Now.AddMinutes(2), Guid.NewGuid());

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [Fact]
    public async Task RefreshTokens_ExpiryColumns_ShouldBeDateTimeOffset()
    {
        // datetimeoffset, not datetime2: a token issued in one offset and checked in another must
        // compare correctly, and the application compares against DateTimeOffset.
        var columns = await QueryStringsAsync(
            """
            SELECT CONCAT(COLUMN_NAME, '=', DATA_TYPE)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'RefreshTokens'
              AND COLUMN_NAME IN ('CreatedAt', 'ExpiresAt', 'FamilyExpiresAt', 'RotatedAt', 'RevokedAt')
            ORDER BY COLUMN_NAME
            """);

        columns.ShouldBe(
            [
                "CreatedAt=datetimeoffset",
                "ExpiresAt=datetimeoffset",
                "FamilyExpiresAt=datetimeoffset",
                "RevokedAt=datetimeoffset",
                "RotatedAt=datetimeoffset",
            ]);
    }

    [Fact]
    public async Task RefreshTokens_ShouldHaveARowVersionColumn()
    {
        // AggregateRootConvention is not registered on the Identity context, so this column exists
        // only because RefreshTokenConfiguration asks for it explicitly.
        var column = await QueryStringsAsync(
            """
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'RefreshTokens' AND COLUMN_NAME = 'RowVersion'
            """);

        column.ShouldBe(["timestamp"]);
    }

    private RefreshToken NewRoot()
        => RefreshToken.CreateRoot(
            _userId,
            RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken()),
            Now,
            IdleWindow,
            AbsoluteWindow);

    private static RefreshToken NewChild(RefreshToken parent, DateTimeOffset now)
        => parent.CreateChild(
            RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken()),
            now,
            IdleWindow);

    private static int UniqueViolationNumberOf(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;
        sqlException.ShouldNotBeNull($"Expected a SqlException, got: {exception.InnerException}");

        return sqlException.Number;
    }

    private Task<List<string>> QueryIndexAsync(string indexName)
        => QueryStringsAsync(
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
            WHERE s.name = 'auth' AND t.name = 'RefreshTokens' AND i.name = @p0
            ORDER BY ic.key_ordinal
            """,
            indexName);

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
