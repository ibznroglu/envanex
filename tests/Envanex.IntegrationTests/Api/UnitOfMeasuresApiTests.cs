using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class UnitOfMeasuresApiTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private HttpClient _client = null!;

    public UnitOfMeasuresApiTests(SqlServerFixture fixture)
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

    public static TheoryData<decimal> ValidDerivedFactors => new()
    {
        0.000001m,
        999999999999.999999m,
        1.500000000m,
    };

    private async Task<Guid> SeedBaseUnitViaApiAsync(string code = "ADET", string name = "Adet")
    {
        var command = new CreateUnitOfMeasureCommand(code, name, null, 1m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> SeedInactiveBaseUnitAsync(string code = "PASIF", string name = "Pasif Birim")
    {
        var uom = UnitOfMeasure.Create(code, name, null, 1m).Value;
        uom.Deactivate();

        await using var context = _fixture.CreateDbContext();
        context.UnitOfMeasures.Add(uom);
        await context.SaveChangesAsync();

        return uom.Id;
    }

    private static async Task<string[]> ReadFieldValidationMessagesAsync(HttpResponseMessage response, string fieldName)
    {
        // The status comes first, so a non-JSON 500 fails on this assertion rather than on a parse.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().ShouldBe("Validation Failed");
        body.TryGetProperty("errors", out var errors).ShouldBeTrue();
        errors.TryGetProperty(fieldName, out var fieldErrors).ShouldBeTrue();

        return [.. fieldErrors.EnumerateArray().Select(e => e.GetString()!)];
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithValidPayload_ShouldReturn201WithLocationHeader()
    {
        var command = new CreateUnitOfMeasureCommand("KG", "Kilogram", null, 1m);

        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.PathAndQuery.ShouldContain("/api/unit-of-measures/");

        var id = await response.Content.ReadFromJsonAsync<Guid>();
        id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithDuplicateCode_ShouldReturn409()
    {
        await SeedBaseUnitViaApiAsync("DUP-UOM", "Duplicate UoM");

        var command = new CreateUnitOfMeasureCommand("DUP-UOM", "Another UoM", null, 1m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(409);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithInvalidPayload_ShouldReturn400WithProblemDetails()
    {
        // Empty code triggers validation failure
        var command = new CreateUnitOfMeasureCommand("", "Valid Name", null, 1m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Validation Failed");
        body.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithNonExistentBaseUnit_ShouldReturn422()
    {
        var command = new CreateUnitOfMeasureCommand("DERIVED", "Derived Unit", Guid.NewGuid(), 0.001m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(422);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithInactiveBaseUnit_ShouldReturn422()
    {
        var inactiveId = await SeedInactiveBaseUnitAsync();

        var command = new CreateUnitOfMeasureCommand("DERIVED2", "Derived From Inactive", inactiveId, 0.5m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(422);
    }

    [Fact]
    public async Task GetUnitOfMeasureById_WithExistingUnit_ShouldReturn200()
    {
        var id = await SeedBaseUnitViaApiAsync("METRE", "Metre");

        var response = await _client.GetAsync($"/api/unit-of-measures/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("code").GetString().ShouldBe("METRE");
        body.GetProperty("name").GetString().ShouldBe("Metre");
    }

    [Fact]
    public async Task GetUnitOfMeasureById_WithNonExistentId_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/unit-of-measures/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task ListUnitOfMeasures_ShouldReturnAllUnits()
    {
        await SeedBaseUnitViaApiAsync("LIST-A", "Unit A");
        await SeedBaseUnitViaApiAsync("LIST-B", "Unit B");

        var response = await _client.GetAsync("/api/unit-of-measures");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithDerivedFactorBeyondSixDecimalPlaces_ShouldReturn400WithTurkishMessage()
    {
        var baseId = await SeedBaseUnitViaApiAsync();

        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, 0.0000001m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        var messages = await ReadFieldValidationMessagesAsync(response, "ConversionFactor");
        messages.ShouldBe(["D\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 en fazla 6 ondal\u0131k basamak i\u00e7erebilir; sondaki s\u0131f\u0131rlar say\u0131lmaz."]);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithDerivedFactorAboveMaximum_ShouldReturn400WithTurkishMessage()
    {
        var baseId = await SeedBaseUnitViaApiAsync();

        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, 1000000000000m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        var messages = await ReadFieldValidationMessagesAsync(response, "ConversionFactor");
        messages.ShouldBe(["D\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 en fazla 999.999.999.999,999999 olabilir."]);
    }

    [Fact]
    public async Task CreateUnitOfMeasure_WithDerivedFactorTooPreciseAndAboveMaximum_ShouldReturn400WithBothTurkishMessages()
    {
        var baseId = await SeedBaseUnitViaApiAsync();

        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, 1000000000000.0000001m);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        var messages = await ReadFieldValidationMessagesAsync(response, "ConversionFactor");
        messages.ShouldBe(
        [
            "D\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 en fazla 6 ondal\u0131k basamak i\u00e7erebilir; sondaki s\u0131f\u0131rlar say\u0131lmaz.",
            "D\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 en fazla 999.999.999.999,999999 olabilir.",
        ]);
    }

    [Theory]
    [MemberData(nameof(ValidDerivedFactors))]
    public async Task CreateUnitOfMeasure_WithValidDerivedFactor_ShouldReturn201AndGetShouldReturnIt(decimal factor)
    {
        var baseId = await SeedBaseUnitViaApiAsync();

        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, factor);
        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();

        var getResponse = await _client.GetAsync($"/api/unit-of-measures/{id}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("conversionFactor").GetDecimal().ShouldBe(factor);
        body.GetProperty("baseUnitId").GetGuid().ShouldBe(baseId);
    }
}
