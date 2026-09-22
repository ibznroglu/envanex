using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class ConcurrencyApiTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private HttpClient _client = null!;

    public ConcurrencyApiTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = await _fixture.CreateAdministratorClientAsync();
    }

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

    private async Task<Guid> SeedProductViaApiAsync(Guid unitOfMeasureId, string code = "CONC-001", string name = "Concurrency Product")
    {
        var command = new CreateProductCommand(code, name, unitOfMeasureId, 100m, "TRY", 10m);
        var response = await _client.PostAsJsonAsync("/api/products", command);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<JsonElement> GetProductDetailAsync(Guid productId)
    {
        var response = await _client.GetAsync($"/api/products/{productId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task UpdateProduct_WithStaleRowVersion_ThroughFullStack_ShouldReturn409()
    {
        // Arrange: create a product and get its initial RowVersion (v1)
        var uomId = await SeedUnitOfMeasureAsync();
        var productId = await SeedProductViaApiAsync(uomId);
        var detail = await GetProductDetailAsync(productId);
        var rowVersionV1 = Convert.FromBase64String(detail.GetProperty("rowVersion").GetString()!);

        // Act 1: PUT with v1 should succeed (200), producing v2
        var updateCommand1 = new UpdateProductCommand(
            productId, "Updated Name V2", uomId, 200m, "TRY", 20m, rowVersionV1);
        var response1 = await _client.PutAsJsonAsync($"/api/products/{productId}", updateCommand1);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act 2: PUT again with stale v1 should return 409 Conflict
        var updateCommand2 = new UpdateProductCommand(
            productId, "Updated Name V3", uomId, 300m, "TRY", 30m, rowVersionV1);
        var response2 = await _client.PutAsJsonAsync($"/api/products/{productId}", updateCommand2);

        // Assert
        response2.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await response2.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(409);
    }

    [Fact]
    public async Task ActivateProduct_WithStaleRowVersion_ShouldReturn409()
    {
        // Arrange: create a product and deactivate it to get a fresh RowVersion
        var uomId = await SeedUnitOfMeasureAsync();
        var productId = await SeedProductViaApiAsync(uomId);
        var detail = await GetProductDetailAsync(productId);
        var rowVersionV1 = Convert.FromBase64String(detail.GetProperty("rowVersion").GetString()!);

        // Deactivate the product to advance the RowVersion
        var deactivateCmd = new DeactivateProductCommand(productId, rowVersionV1);
        var deactivateResponse = await _client.PostAsJsonAsync($"/api/products/{productId}/deactivate", deactivateCmd);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now try to activate with the stale v1 RowVersion
        var activateCmd = new ActivateProductCommand(productId, rowVersionV1);
        var activateResponse = await _client.PostAsJsonAsync($"/api/products/{productId}/activate", activateCmd);

        // Assert: should return 409 because the RowVersion has changed
        activateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await activateResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(409);
    }
}
