using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Application.Tests.Products.Commands;

public class UpdateProductCommandHandlerTests
{
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfMeasureRepository _unitOfMeasureRepository = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly UpdateProductCommandHandler _handler;

    private static readonly Guid ActiveUnitOfMeasureId = Guid.CreateVersion7();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    public UpdateProductCommandHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_productRepository);
        _unitOfMeasureRepository.SeedActiveStatus(ActiveUnitOfMeasureId, true);
        _handler = new UpdateProductCommandHandler(
            _productRepository, _unitOfMeasureRepository, _unitOfWork);
    }

    private Product SeedProduct()
    {
        Product product = Product.Create("PRD001", "Existing Product", ActiveUnitOfMeasureId,
            Money.Of(50m, Currency.TRY).Value, Quantity.Of(5m).Value).Value;
        _productRepository.Seed(product);
        return product;
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldReturnProductId()
    {
        Product product = SeedProduct();
        var command = new UpdateProductCommand(
            product.Id, "Updated Name", ActiveUnitOfMeasureId, 200m, "TRY", 20m, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(product.Id);
        product.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError()
    {
        var command = new UpdateProductCommand(
            Guid.CreateVersion7(), "Name", ActiveUnitOfMeasureId, 100m, "TRY", 10m, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentUnitOfMeasure_ShouldReturnUnitOfMeasureNotFoundError()
    {
        Product product = SeedProduct();
        Guid nonExistentId = Guid.CreateVersion7();
        var command = new UpdateProductCommand(
            product.Id, "Name", nonExistentId, 100m, "TRY", 10m, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureNotFound);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUnitOfMeasure_ShouldReturnUnitOfMeasureInactiveError()
    {
        Product product = SeedProduct();
        Guid inactiveId = Guid.CreateVersion7();
        _unitOfMeasureRepository.SeedActiveStatus(inactiveId, false);
        var command = new UpdateProductCommand(
            product.Id, "Name", inactiveId, 100m, "TRY", 10m, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureInactive);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSetOriginalRowVersion()
    {
        Product product = SeedProduct();
        var command = new UpdateProductCommand(
            product.Id, "Updated", ActiveUnitOfMeasureId, 100m, "TRY", 10m, RowVersion);

        await _handler.HandleAsync(command);

        _productRepository.OriginalRowVersions.ShouldContainKey(product.Id);
        _productRepository.OriginalRowVersions[product.Id].ShouldBe(RowVersion);
    }

    [Fact]
    public async Task HandleAsync_WhenUnitOfWorkThrowsConcurrencyConflict_ShouldReturnConcurrencyConflictError()
    {
        Product product = SeedProduct();
        _unitOfWork.ThrowOnSaveChanges(new ConcurrencyConflictException());
        var command = new UpdateProductCommand(
            product.Id, "Updated", ActiveUnitOfMeasureId, 100m, "TRY", 10m, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task HandleAsync_ShouldSetRowVersionBeforeSavingChanges()
    {
        Product product = SeedProduct();
        var command = new UpdateProductCommand(
            product.Id, "Updated", ActiveUnitOfMeasureId, 100m, "TRY", 10m, RowVersion);

        await _handler.HandleAsync(command);

        _unitOfWork.RowVersionWasSetBeforeSave.ShouldNotBeNull();
        _unitOfWork.RowVersionWasSetBeforeSave.Value.ShouldBeTrue();
    }
}
