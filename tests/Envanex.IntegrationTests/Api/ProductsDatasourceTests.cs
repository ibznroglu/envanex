using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class ProductsDatasourceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly HttpClient _client;

    public ProductsDatasourceTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _client = fixture.WebApplicationFactory.CreateClient();
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> SeedUnitOfMeasureAsync(string code = "ADET", string name = "Adet")
    {
        var uom = UnitOfMeasure.Create(code, name, null, 1m).Value;

        await using var context = _fixture.CreateDbContext();
        context.UnitOfMeasures.Add(uom);
        await context.SaveChangesAsync();

        return uom.Id;
    }

    private async Task SeedProductViaApiAsync(Guid unitOfMeasureId, string code, string name)
    {
        var command = new CreateProductCommand(code, name, unitOfMeasureId, 100m, "TRY", 10m);
        var response = await _client.PostAsJsonAsync("/api/products", command);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetDatasource_WithDefaultRequest_ShouldReturn200WithData()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "DS-001", "DataSource Product");

        var response = await _client.GetAsync("/api/products/datasource");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetDatasource_WithNoTake_ShouldDefaultTo20()
    {
        var uomId = await SeedUnitOfMeasureAsync();

        // Seed 25 products so we can verify the default take of 20
        for (int i = 1; i <= 25; i++)
        {
            await SeedProductViaApiAsync(uomId, $"TAKE-{i:D3}", $"Product {i}");
        }

        var response = await _client.GetAsync("/api/products/datasource?requireTotalCount=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        // Default take is 20, so at most 20 items returned
        data.GetArrayLength().ShouldBe(20);

        // Total count should be 25 (all seeded products)
        body.GetProperty("totalCount").GetInt32().ShouldBe(25);
    }

    [Fact]
    public async Task GetDatasource_WithTakeExceedingMax_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?take=200");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task GetDatasource_WithAllowedSortField_ShouldReturn200()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "SORT-001", "Sort Product");

        // "Code" is in the sort allowlist
        var response = await _client.GetAsync("/api/products/datasource?sort=[{\"selector\":\"Code\",\"desc\":false}]");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDatasource_WithDisallowedSortField_ShouldReturn400()
    {
        // "RowVersion" is not in the sort allowlist
        var response = await _client.GetAsync("/api/products/datasource?sort=[{\"selector\":\"RowVersion\",\"desc\":false}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task GetDatasource_WithDisallowedGroupField_ShouldReturn400()
    {
        // "Code" is in sort allowlist but NOT in group allowlist
        var response = await _client.GetAsync("/api/products/datasource?group=[{\"selector\":\"Code\",\"desc\":false}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task GetDatasource_WithRequireGroupCount_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?requireGroupCount=true");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task GetDatasource_WithGroupSummary_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?groupSummary=[{\"selector\":\"ListPriceAmount\",\"summaryType\":\"sum\"}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task GetDatasource_WithSkipAndTake_ShouldReturnCorrectPage()
    {
        var uomId = await SeedUnitOfMeasureAsync();

        for (int i = 1; i <= 50; i++)
        {
            await SeedProductViaApiAsync(uomId, $"PAGE-{i:D3}", $"Paged Product {i}");
        }

        var response = await _client.GetAsync("/api/products/datasource?skip=10&take=5&requireTotalCount=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        data.GetArrayLength().ShouldBe(5);
        body.GetProperty("totalCount").GetInt32().ShouldBe(50);
    }

    [Fact]
    public async Task Datasource_Query_ShouldTranslateOrderByAndPagingToSql()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "REGR-001", "Regression Guard Product");

        using var scope = _fixture.WebApplicationFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProductReadRepository>();

        // Compose exactly what DataSourceLoader composes: an OrderBy over a projected field,
        // plus Skip/Take paging.
        var query = repo.GetAll()
            .OrderBy(p => p.Code)
            .Skip(0)
            .Take(10);

        // ToQueryString() shows what actually reaches SQL Server. A silent fall back to
        // client evaluation does not throw, so asserting "does not throw" proves nothing —
        // only the generated SQL does.
        var sql = query.ToQueryString();

        sql.ShouldContain("ORDER BY");
        sql.ShouldContain("OFFSET");
        sql.ShouldContain("FETCH NEXT");
    }

    /// <summary>
    /// One test case per field in the endpoint's sort/filter allowlist. Deriving the cases from
    /// <see cref="ProductsController.AllowedDataSourceFields"/> instead of restating them keeps
    /// the two lists from drifting apart — the drift that let a 500 on ListPriceCurrency and
    /// ReorderPoint reach production.
    /// </summary>
    public static TheoryData<string> AllowlistedFields()
    {
        var data = new TheoryData<string>();
        foreach (var field in ProductsController.AllowedDataSourceFields)
        {
            data.Add(field);
        }

        return data;
    }

    /// <summary>
    /// Filter values keyed by allowlisted field. A field with no entry here yields a null value,
    /// which the test asserts on and fails — see <see cref="AllowlistedFieldsWithFilterValues"/>.
    /// </summary>
    private static readonly Dictionary<string, string> FilterValuesByField = new(StringComparer.Ordinal)
    {
        ["Code"] = "\"TFILT-001\"",
        ["Name"] = "\"Translatable Filter Product\"",
        ["UnitOfMeasureName"] = "\"Adet\"",
        ["ListPriceAmount"] = "0",
        ["IsActive"] = "true",
    };

    /// <summary>
    /// Every allowlisted field paired with its filter value, or null when none is defined.
    /// The unmatched field is still emitted as a case rather than dropped, so adding a field to
    /// the allowlist without adding a filter value here produces a red test naming that field.
    /// </summary>
    public static TheoryData<string, string?> AllowlistedFieldsWithFilterValues()
    {
        var data = new TheoryData<string, string?>();
        foreach (var field in ProductsController.AllowedDataSourceFields)
        {
            data.Add(field, FilterValuesByField.GetValueOrDefault(field));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllowlistedFields))]
    public async Task Datasource_EverySortableFieldInAllowlist_ShouldReturn200(string field)
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "TSORT-001", "Translatable Sort Product");

        var response = await _client.GetAsync(
            $"/api/products/datasource?sort=[{{\"selector\":\"{field}\",\"desc\":false}}]");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// One test case per field in the endpoint's group allowlist, derived from
    /// <see cref="ProductsController.AllowedDataSourceGroupFields"/> for the same reason as
    /// <see cref="AllowlistedFields"/>. The group path had no derived coverage at all, so the
    /// drift that shipped a 500 on the sort path was still open here.
    /// </summary>
    public static TheoryData<string> AllowlistedGroupFields()
    {
        var data = new TheoryData<string>();
        foreach (var field in ProductsController.AllowedDataSourceGroupFields)
        {
            data.Add(field);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllowlistedFieldsWithFilterValues))]
    public async Task Datasource_EveryFilterableFieldInAllowlist_ShouldReturn200(string field, string? value)
    {
        value.ShouldNotBeNull(
            $"'{field}' is in ProductsController.AllowedDataSourceFields but has no filter value in "
            + $"{nameof(FilterValuesByField)}. Add an entry for it so the field is actually covered.");

        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "TFILT-001", "Translatable Filter Product");

        // ListPriceAmount is compared with ">" so the seeded row (100) matches; the rest use "=".
        var op = string.Equals(field, "ListPriceAmount", StringComparison.Ordinal) ? ">" : "=";
        var response = await _client.GetAsync(
            $"/api/products/datasource?filter=[\"{field}\",\"{op}\",{value}]");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(AllowlistedGroupFields))]
    public async Task Datasource_EveryGroupableFieldInAllowlist_ShouldReturn200(string field)
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "TGROUP-001", "Translatable Group Product");

        var response = await _client.GetAsync(
            $"/api/products/datasource?group=[{{\"selector\":\"{field}\",\"desc\":false,\"isExpanded\":true}}]");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("ListPriceCurrency")]
    [InlineData("ReorderPoint")]
    public async Task Datasource_SortOnValueConvertedField_ShouldReturn400NotServerError(string field)
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "VCSORT-001", "Value Converted Sort Product");

        // These fields map through value converters (Quantity, Currency). EF Core cannot
        // translate DataSourceLoader's OrderBy composition over them and throws, so they are
        // not in the allowlist. Putting them back without fixing the mapping turns this red.
        var response = await _client.GetAsync(
            $"/api/products/datasource?sort=[{{\"selector\":\"{field}\",\"desc\":false}}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Bad Request");
    }

    [Theory]
    [InlineData("ListPriceCurrency", "\"TRY\"")]
    [InlineData("ReorderPoint", "0")]
    public async Task Datasource_FilterOnValueConvertedField_ShouldReturn400NotServerError(string field, string value)
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "VCFILT-001", "Value Converted Filter Product");

        // Same reason as the sort case: the guard must reject before EF Core can throw.
        var response = await _client.GetAsync(
            $"/api/products/datasource?filter=[\"{field}\",\"=\",{value}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Bad Request");
    }

    [Fact]
    public async Task Datasource_WithoutSort_ShouldApplyDeterministicDefaultOrder()
    {
        var uomId = await SeedUnitOfMeasureAsync();

        for (int i = 1; i <= 6; i++)
        {
            await SeedProductViaApiAsync(uomId, $"ORDER-{i:D3}", $"Ordered Product {i}");
        }

        // No sort parameter: the guard must supply one so OFFSET/FETCH is deterministic.
        var firstPage = await _client.GetAsync("/api/products/datasource?skip=0&take=2");
        var secondPage = await _client.GetAsync("/api/products/datasource?skip=2&take=2");

        firstPage.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstIds = await ReadIdsAsync(firstPage);
        var secondIds = await ReadIdsAsync(secondPage);

        firstIds.Count.ShouldBe(2);
        secondIds.Count.ShouldBe(2);
        firstIds.Intersect(secondIds).ShouldBeEmpty();
    }

    [Fact]
    public async Task Datasource_SortingByNonUniqueField_ShouldReturnStablePagesAcrossOffsets()
    {
        var uomId = await SeedUnitOfMeasureAsync();

        // Every product created through the API is active, so "IsActive" is one repeated key
        // across all six rows and cannot order them on its own. Codes are seeded in descending
        // order so ascending Code order is not the insertion order — a page that merely comes
        // back in physical order cannot satisfy the assertions below by accident.
        for (int i = 6; i >= 1; i--)
        {
            await SeedProductViaApiAsync(uomId, $"STABLE-{i:D3}", $"Stable Paging Product {i}");
        }

        const string sort = "sort=[{\"selector\":\"IsActive\",\"desc\":false}]";
        var firstPage = await _client.GetAsync($"/api/products/datasource?{sort}&skip=0&take=3");
        var secondPage = await _client.GetAsync($"/api/products/datasource?{sort}&skip=3&take=3");

        firstPage.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstRows = await ReadRowsAsync(firstPage);
        var secondRows = await ReadRowsAsync(secondPage);

        firstRows.Count.ShouldBe(3);
        secondRows.Count.ShouldBe(3);

        var firstIds = firstRows.Select(r => r.Id).ToList();
        var secondIds = secondRows.Select(r => r.Id).ToList();

        // No row may appear on both pages, and no row may be skipped: together the two pages
        // are exactly the six seeded products.
        firstIds.Intersect(secondIds).ShouldBeEmpty();
        firstIds.Concat(secondIds).Distinct().Count().ShouldBe(6);

        // The Code tiebreaker is what makes that hold. Without it the ordering is the single
        // repeated IsActive key and the split between the pages is whatever the plan produces,
        // so asserting the exact Code sequence is what turns a removed tiebreaker red.
        var codes = firstRows.Concat(secondRows).Select(r => r.Code).ToList();
        codes.ShouldBe(Enumerable.Range(1, 6).Select(i => $"STABLE-{i:D3}"));
    }

    private static async Task<List<string>> ReadIdsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return [.. body.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("id").GetString()!)];
    }

    private static async Task<List<(string Id, string Code)>> ReadRowsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return
        [
            .. body.GetProperty("data").EnumerateArray().Select(e => (
                Id: e.GetProperty("id").GetString()!,
                Code: e.GetProperty("code").GetString()!))
        ];
    }

    [Fact]
    public async Task Datasource_WithNegativeTake_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?take=-1");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Datasource_WithNegativeSkip_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?skip=-1");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Datasource_WithExcessiveSkip_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/products/datasource?skip=100000");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Datasource_WithMalformedFilter_ShouldReturn400NotServerError()
    {
        // Send a malformed filter value that cannot be parsed
        var response = await _client.GetAsync("/api/products/datasource?filter=[[[invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Datasource_WithZeroTake_ShouldApplyDefault()
    {
        var uomId = await SeedUnitOfMeasureAsync();

        // Seed a few products so we can verify data is returned
        for (int i = 1; i <= 3; i++)
        {
            await SeedProductViaApiAsync(uomId, $"ZERO-{i:D3}", $"Zero Take Product {i}");
        }

        // take=0 means "not specified" — the guard should apply defaultTake (20)
        var response = await _client.GetAsync("/api/products/datasource?take=0");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Datasource_WithDisallowedFilterField_ShouldReturn400()
    {
        // "RowVersion" is not in the allowlist — must return 400, not 500
        var response = await _client.GetAsync(
            "/api/products/datasource?filter=[\"RowVersion\",\"=\",\"abc\"]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Datasource_WithAllowedFilterField_ShouldReturn200()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "FILT-001", "Filter Product");

        // "Code" is in the allowlist — should succeed
        var response = await _client.GetAsync(
            "/api/products/datasource?filter=[\"Code\",\"=\",\"FILT-001\"]");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Datasource_WithNestedDisallowedFilterField_ShouldReturn400()
    {
        // Nested filter: [["Code","=","X"],"and",["RowVersion","=","Y"]]
        // "RowVersion" is not in the allowlist — proves recursive checking works
        var response = await _client.GetAsync(
            "/api/products/datasource?filter=[[\"Code\",\"=\",\"X\"],\"and\",[\"RowVersion\",\"=\",\"Y\"]]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Datasource_WithCombinedSortGroupAndFilter_ShouldReturn200()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "COMBO-001", "Combo Product");

        // Sort by "Code", group by "IsActive", filter by "Name" — all allowed fields
        var url = "/api/products/datasource"
            + "?sort=[{\"selector\":\"Code\",\"desc\":false}]"
            + "&group=[{\"selector\":\"IsActive\",\"desc\":false,\"isExpanded\":true}]"
            + "&filter=[\"Name\",\"contains\",\"Combo\"]";

        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
