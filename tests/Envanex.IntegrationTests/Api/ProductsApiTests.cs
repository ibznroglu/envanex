using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class ProductsApiTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly HttpClient _client;

    public ProductsApiTests(SqlServerFixture fixture)
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

    private async Task<Guid> SeedProductViaApiAsync(Guid unitOfMeasureId, string code = "PROD-001", string name = "Test Product")
    {
        var command = new CreateProductCommand(code, name, unitOfMeasureId, 100m, "TRY", 10m);
        var response = await _client.PostAsJsonAsync("/api/products", command);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task GetProductById_WithExistingProduct_ShouldReturn200WithProductDetail()
    {
        var uomId = await SeedUnitOfMeasureAsync();
        var productId = await SeedProductViaApiAsync(uomId);

        var response = await _client.GetAsync($"/api/products/{productId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(productId);
        body.GetProperty("code").GetString().ShouldBe("PROD-001");
        body.GetProperty("name").GetString().ShouldBe("Test Product");
        body.GetProperty("unitOfMeasureName").GetString().ShouldBe("Adet");
        body.GetProperty("rowVersion").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProductById_WithNonExistentId_ShouldReturn404WithProblemDetails()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(404);
        body.GetProperty("title").GetString().ShouldBe("Not Found");
    }

    [Fact]
    public async Task GetProductById_ResponseHeaders_ShouldContainXContentTypeOptions()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.Headers.TryGetValues("X-Content-Type-Options", out var values).ShouldBeTrue();
        values.ShouldContain("nosniff");
    }

    [Fact]
    public async Task GetProductById_ResponseHeaders_ShouldContainXFrameOptions()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.Headers.TryGetValues("X-Frame-Options", out var values).ShouldBeTrue();
        values.ShouldContain("DENY");
    }

    [Fact]
    public async Task GetProductById_ResponseHeaders_ShouldContainReferrerPolicy()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.Headers.TryGetValues("Referrer-Policy", out var values).ShouldBeTrue();
        values.ShouldContain("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task ValidationError_ShouldReturnTurkishMessageInProblemDetails()
    {
        // Send a POST with empty Code to trigger validation
        var command = new CreateProductCommand("", "Valid Name", Guid.NewGuid(), 100m, "TRY", 10m);
        var response = await _client.PostAsJsonAsync("/api/products", command);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Validation Failed");

        var errors = body.GetProperty("errors");
        var codeErrors = errors.GetProperty("Code");
        codeErrors.GetArrayLength().ShouldBeGreaterThan(0);

        // The Turkish message for Product.CodeRequired
        codeErrors[0].GetString().ShouldBe("\u00dcr\u00fcn kodu zorunludur.");
    }
}
