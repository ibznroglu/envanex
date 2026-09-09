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
        body.GetProperty("detail").GetString().ShouldBe("Bir veya daha fazla do\u011frulama hatas\u0131 olu\u015ftu.");

        var errors = body.GetProperty("errors");
        var codeErrors = errors.GetProperty("Code");
        codeErrors.GetArrayLength().ShouldBeGreaterThan(0);

        // The Turkish message for Product.CodeRequired
        codeErrors[0].GetString().ShouldBe("\u00dcr\u00fcn kodu zorunludur.");
    }

    [Fact]
    public async Task CreateProduct_WithMultipleInvalidFields_ShouldReturnAllFieldsInErrorsDictionary()
    {
        // Empty Code, empty Name, and negative ReorderPoint each trigger a separate validation error
        var command = new CreateProductCommand("", "", Guid.NewGuid(), 100m, "TRY", -1m);
        var response = await _client.PostAsJsonAsync("/api/products", command);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);

        var errors = body.GetProperty("errors");

        // Should have entries for Code, Name, and ReorderPoint (PropertyName keys, not ErrorCodes)
        errors.TryGetProperty("Code", out _).ShouldBeTrue("Expected 'Code' key in errors dictionary");
        errors.TryGetProperty("Name", out _).ShouldBeTrue("Expected 'Name' key in errors dictionary");
        errors.TryGetProperty("ReorderPoint", out _).ShouldBeTrue("Expected 'ReorderPoint' key in errors dictionary");

        // Each entry should contain a Turkish message, not an error code
        errors.GetProperty("Code")[0].GetString().ShouldBe("\u00dcr\u00fcn kodu zorunludur.");
        errors.GetProperty("Name")[0].GetString().ShouldBe("\u00dcr\u00fcn ad\u0131 zorunludur.");
        errors.GetProperty("ReorderPoint")[0].GetString().ShouldBe("Yeniden sipari\u015f noktas\u0131 negatif olamaz.");
    }

    [Fact]
    public async Task CreateProduct_WithNonExistentUnitOfMeasure_ShouldReturn422()
    {
        // A random Guid that doesn't exist as a UnitOfMeasure in the database.
        // The handler returns ProductErrors.UnitOfMeasureNotFound which maps to 422.
        var command = new CreateProductCommand("UOM-TEST", "UoM Test Product", Guid.NewGuid(), 100m, "TRY", 10m);
        var response = await _client.PostAsJsonAsync("/api/products", command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(422);
        body.GetProperty("title").GetString().ShouldBe("Unprocessable Entity");
        body.GetProperty("detail").GetString().ShouldBe("Belirtilen \u00f6l\u00e7\u00fc birimi bulunamad\u0131.");
    }
}
