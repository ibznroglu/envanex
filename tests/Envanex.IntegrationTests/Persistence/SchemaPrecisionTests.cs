using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaPrecisionTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public SchemaPrecisionTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MoneyColumn_ShouldBeDecimal18_4()
    {
        await using var context = _fixture.CreateDbContext();

        var result = await context.Database
            .SqlQueryRaw<ColumnInfo>(
                """
                SELECT NUMERIC_PRECISION AS Precision, NUMERIC_SCALE AS Scale
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'TestProducts'
                  AND COLUMN_NAME = 'UnitPrice_Amount'
                """)
            .SingleAsync();

        ((int)result.Precision).ShouldBe(18);
        result.Scale.ShouldBe(4);
    }

    [Fact]
    public async Task QuantityColumn_ShouldBeDecimal18_6()
    {
        await using var context = _fixture.CreateDbContext();

        var result = await context.Database
            .SqlQueryRaw<ColumnInfo>(
                """
                SELECT NUMERIC_PRECISION AS Precision, NUMERIC_SCALE AS Scale
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'TestProducts'
                  AND COLUMN_NAME = 'StockQuantity'
                """)
            .SingleAsync();

        ((int)result.Precision).ShouldBe(18);
        result.Scale.ShouldBe(6);
    }

    // EF Core's SqlQueryRaw requires a type with a parameterless constructor.
    // This class is needed to materialize INFORMATION_SCHEMA results.
    private sealed class ColumnInfo
    {
        public byte Precision { get; set; }
        public int Scale { get; set; }
    }
}
