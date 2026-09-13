using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.ValueObjects;
using Envanex.Infrastructure.Persistence;
using Envanex.Infrastructure.Persistence.Repositories;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class RepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public RepositoryTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Money DefaultPrice => Money.Of(100m, Currency.TRY).Value;
    private static Quantity DefaultQuantity => Quantity.Of(10m).Value;

    private async Task<UnitOfMeasure> SeedUnitOfMeasureAsync(string code = "ADET", string name = "Adet")
    {
        var uom = UnitOfMeasure.Create(code, name, null, 1m).Value;

        await using var context = _fixture.CreateDbContext();
        context.UnitOfMeasures.Add(uom);
        await context.SaveChangesAsync();

        return uom;
    }

    private async Task<Product> SeedProductAsync(Guid unitOfMeasureId, string code = "PROD-001", string name = "Test Product")
    {
        var product = Product.Create(code, name, unitOfMeasureId, DefaultPrice, DefaultQuantity).Value;

        await using var context = _fixture.CreateDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product;
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_WithStaleRowVersion_ShouldThrowConcurrencyConflictException()
    {
        var uom = await SeedUnitOfMeasureAsync();
        var product = await SeedProductAsync(uom.Id);

        await using var context1 = _fixture.CreateDbContext();
        await using var context2 = _fixture.CreateDbContext();

        var product1 = await context1.Products.SingleAsync(p => p.Id == product.Id);
        var product2 = await context2.Products.SingleAsync(p => p.Id == product.Id);

        // Save in context1 first
        product1.UpdatePrice(Money.Of(200m, Currency.TRY).Value);
        await context1.SaveChangesAsync();

        // Now try saving in context2 with stale RowVersion through UnitOfWork
        product2.UpdatePrice(Money.Of(300m, Currency.TRY).Value);
        var unitOfWork = new UnitOfWork(context2);

        await Should.ThrowAsync<ConcurrencyConflictException>(
            () => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_WithDuplicateCode_ShouldThrowDuplicateKeyException()
    {
        var uom = await SeedUnitOfMeasureAsync();
        await SeedProductAsync(uom.Id, "UNIQUE-CODE");

        var duplicate = Product.Create("UNIQUE-CODE", "Duplicate Product", uom.Id, DefaultPrice, DefaultQuantity).Value;

        await using var context = _fixture.CreateDbContext();
        context.Products.Add(duplicate);
        var unitOfWork = new UnitOfWork(context);

        await Should.ThrowAsync<DuplicateKeyException>(
            () => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductRepository_SetOriginalRowVersion_WithStaleValue_ShouldCauseConcurrencyConflict()
    {
        var uom = await SeedUnitOfMeasureAsync();
        var product = await SeedProductAsync(uom.Id);

        // Load product and capture its RowVersion
        byte[] originalRowVersion;
        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            originalRowVersion = context.Entry(loaded).Property<byte[]>("RowVersion").CurrentValue;
        }

        // Update product in another context to change RowVersion
        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.UpdatePrice(Money.Of(200m, Currency.TRY).Value);
            await context.SaveChangesAsync();
        }

        // Now try updating with the stale RowVersion using repository + UnitOfWork
        await using var finalContext = _fixture.CreateDbContext();
        var repo = new ProductRepository(finalContext);
        var unitOfWork = new UnitOfWork(finalContext);

        var productToUpdate = await repo.GetByIdAsync(product.Id);
        productToUpdate.ShouldNotBeNull();
        productToUpdate.UpdatePrice(Money.Of(300m, Currency.TRY).Value);

        // Set the STALE RowVersion so EF thinks the original was different
        repo.SetOriginalRowVersion(productToUpdate, originalRowVersion);

        await Should.ThrowAsync<ConcurrencyConflictException>(
            () => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductRepository_ExistsByCodeAsync_ExistingCode_ShouldReturnTrue()
    {
        var uom = await SeedUnitOfMeasureAsync();
        await SeedProductAsync(uom.Id, "EXISTS-TEST");

        await using var context = _fixture.CreateDbContext();
        var repo = new ProductRepository(context);

        var exists = await repo.ExistsByCodeAsync("EXISTS-TEST");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ProductReadRepository_GetAll_ShouldJoinUnitOfMeasureName()
    {
        var uom = await SeedUnitOfMeasureAsync("KG", "Kilogram");
        await SeedProductAsync(uom.Id);

        await using var context = _fixture.CreateDbContext();
        var readRepo = new ProductReadRepository(context);

        var items = await readRepo.GetAll().ToListAsync();

        items.ShouldNotBeEmpty();
        items.First().UnitOfMeasureName.ShouldBe("Kilogram");
    }

    [Fact]
    public async Task ProductReadRepository_GetByIdAsync_ShouldReturnDetailDto()
    {
        var uom = await SeedUnitOfMeasureAsync("M", "Metre");
        var product = await SeedProductAsync(uom.Id, "DETAIL-TEST", "Detail Product");

        await using var context = _fixture.CreateDbContext();
        var readRepo = new ProductReadRepository(context);

        var dto = await readRepo.GetByIdAsync(product.Id);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(product.Id);
        dto.Code.ShouldBe("DETAIL-TEST");
        dto.Name.ShouldBe("Detail Product");
        dto.UnitOfMeasureName.ShouldBe("Metre");
        dto.RowVersion.ShouldNotBeNull();
        dto.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ProductReadRepository_GetByIdAsync_ShouldSurfaceRowVersion()
    {
        var uom = await SeedUnitOfMeasureAsync();
        var product = await SeedProductAsync(uom.Id, "RV-DETAIL", "RowVersion Detail");

        await using var context = _fixture.CreateDbContext();
        var readRepo = new ProductReadRepository(context);

        var dto = await readRepo.GetByIdAsync(product.Id);

        dto.ShouldNotBeNull();
        dto.RowVersion.ShouldNotBeNull();
        dto.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UnitOfMeasureRepository_GetActiveStatusAsync_Active_ShouldReturnTrue()
    {
        var uom = await SeedUnitOfMeasureAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new UnitOfMeasureRepository(context);

        var status = await repo.GetActiveStatusAsync(uom.Id);

        status.ShouldBe(true);
    }

    [Fact]
    public async Task UnitOfMeasureRepository_GetActiveStatusAsync_Inactive_ShouldReturnFalse()
    {
        var uom = UnitOfMeasure.Create("PASIF", "Pasif Birim", null, 1m).Value;
        uom.Deactivate();

        await using (var context = _fixture.CreateDbContext())
        {
            context.UnitOfMeasures.Add(uom);
            await context.SaveChangesAsync();
        }

        await using var readContext = _fixture.CreateDbContext();
        var repo = new UnitOfMeasureRepository(readContext);

        var status = await repo.GetActiveStatusAsync(uom.Id);

        status.ShouldBe(false);
    }

    [Fact]
    public async Task UnitOfMeasureRepository_GetActiveStatusAsync_NonExistent_ShouldReturnNull()
    {
        await using var context = _fixture.CreateDbContext();
        var repo = new UnitOfMeasureRepository(context);

        var status = await repo.GetActiveStatusAsync(Guid.NewGuid());

        status.ShouldBeNull();
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_WithDuplicateCode_ShouldExtractIndexNameNotFullMessage()
    {
        var uom = await SeedUnitOfMeasureAsync();
        await SeedProductAsync(uom.Id, "EXTRACT-TEST");

        var duplicate = Product.Create("EXTRACT-TEST", "Duplicate Product", uom.Id, DefaultPrice, DefaultQuantity).Value;

        await using var context = _fixture.CreateDbContext();
        context.Products.Add(duplicate);
        var unitOfWork = new UnitOfWork(context);

        var ex = await Should.ThrowAsync<DuplicateKeyException>(
            () => unitOfWork.SaveChangesAsync());

        // The constraint name should contain the index name prefix, not the full message
        ex.ConstraintName.ShouldNotBeNull();
        ex.ConstraintName.ShouldContain("IX_");
        // If the full message were returned instead, it would contain spaces
        ex.ConstraintName.ShouldNotContain(" ");
    }

    [Fact]
    public async Task ProductRepository_SetOriginalRowVersion_MustBeCalledBeforeSaveChanges_OtherwiseConcurrencyIsDisabled()
    {
        // This test proves that when SetOriginalRowVersion is NOT called,
        // concurrency control is silently disabled: the save succeeds even
        // though another context has modified the row.

        var uom = await SeedUnitOfMeasureAsync();
        var product = await SeedProductAsync(uom.Id, "TRAP-TEST");

        // Update the product in one context to advance the RowVersion
        await using (var context = _fixture.CreateDbContext())
        {
            var loaded = await context.Products.SingleAsync(p => p.Id == product.Id);
            loaded.UpdatePrice(Money.Of(200m, Currency.TRY).Value);
            await context.SaveChangesAsync();
        }

        // Load the product in a new context (OriginalValue is now the DB's current RowVersion)
        // and update WITHOUT calling SetOriginalRowVersion.
        // This SHOULD conceptually be a conflict, but EF sees the current DB value as OriginalValue
        // so it always matches. The save succeeds silently.
        await using var finalContext = _fixture.CreateDbContext();
        var repo = new ProductRepository(finalContext);
        var unitOfWork = new UnitOfWork(finalContext);

        var productToUpdate = await repo.GetByIdAsync(product.Id);
        productToUpdate.ShouldNotBeNull();
        productToUpdate.UpdatePrice(Money.Of(999m, Currency.TRY).Value);

        // Deliberately NOT calling: repo.SetOriginalRowVersion(productToUpdate, staleRowVersion);
        // The save should succeed because OriginalValue == current DB RowVersion
        var rowsAffected = await unitOfWork.SaveChangesAsync();
        rowsAffected.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_WithForeignKeyViolation_ShouldNotTranslateToDuplicateKey()
    {
        // Create a product with a non-existent UnitOfMeasureId, bypassing the handler's pre-check
        var nonExistentUomId = Guid.CreateVersion7();
        var product = Product.Create("FK-TEST", "FK Violation Product", nonExistentUomId, DefaultPrice, DefaultQuantity).Value;

        await using var context = _fixture.CreateDbContext();
        context.Products.Add(product);
        var unitOfWork = new UnitOfWork(context);

        // FK violation should throw DbUpdateException but NOT be translated to DuplicateKeyException
        var ex = await Should.ThrowAsync<DbUpdateException>(
            () => unitOfWork.SaveChangesAsync());

        ex.ShouldNotBeOfType<DuplicateKeyException>();
    }
}
