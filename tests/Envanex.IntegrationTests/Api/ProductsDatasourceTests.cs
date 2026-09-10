using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevExtreme.AspNet.Data;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
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
    public async Task GetDatasource_LoadAsync_OnIQueryable_ShouldNotThrow()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        await SeedProductViaApiAsync(uomId, "REGR-001", "Regression Guard Product");

        using var scope = _fixture.WebApplicationFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProductReadRepository>();

        var options = new DataSourceLoadOptionsBase
        {
            Take = 10,
            Sort = [new SortingInfo { Selector = "Code", Desc = false }],
        };

        // If RowVersion is added back to the GetAll() projection, this call will throw
        // because EF Core cannot translate DataSourceLoader's OrderBy composition
        // against a Join-projected query that includes EF.Property shadow property access.
        var loadResult = await DataSourceLoader.LoadAsync(repo.GetAll(), options, CancellationToken.None);

        loadResult.ShouldNotBeNull();
        loadResult.data.ShouldNotBeNull();
        loadResult.data.Cast<object>().Count().ShouldBeGreaterThan(0);
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
