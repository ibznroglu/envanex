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

        result.Precision.ShouldBe((byte)18);
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

        result.Precision.ShouldBe((byte)18);
        result.Scale.ShouldBe(6);
    }

    [Fact]
    public async Task RowVersionColumn_ShouldBeRowVersionType()
    {
        await using var context = _fixture.CreateDbContext();

        var result = await context.Database
            .SqlQueryRaw<ColumnTypeInfo>(
                """
                SELECT DATA_TYPE AS DataType
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'TestProducts'
                  AND COLUMN_NAME = 'RowVersion'
                """)
            .SingleAsync();

        result.DataType.ShouldBe("timestamp");
    }

    [Fact]
    public async Task CurrencyColumn_ShouldBeNVarChar3()
    {
        await using var context = _fixture.CreateDbContext();

        var result = await context.Database
            .SqlQueryRaw<ColumnStringInfo>(
                """
                SELECT DATA_TYPE AS DataType, CHARACTER_MAXIMUM_LENGTH AS MaxLength
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'TestProducts'
                  AND COLUMN_NAME = 'UnitPrice_Currency'
                """)
            .SingleAsync();

        result.DataType.ShouldBe("nvarchar");
        result.MaxLength.ShouldBe(3);
    }

    // EF Core's SqlQueryRaw requires a type with a parameterless constructor.
    // These classes are needed to materialize INFORMATION_SCHEMA results.
    private sealed class ColumnInfo
    {
        public byte Precision { get; set; }
        public int Scale { get; set; }
    }

    private sealed class ColumnTypeInfo
    {
        public string DataType { get; set; } = default!;
    }

    private sealed class ColumnStringInfo
    {
        public string DataType { get; set; } = default!;
        public int MaxLength { get; set; }
    }
}
