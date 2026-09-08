using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Application.Tests.Products.Commands;

public class CreateProductCommandHandlerTests
{
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfMeasureRepository _unitOfMeasureRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateProductCommandHandler _handler;

    private static readonly Guid ActiveUnitOfMeasureId = Guid.CreateVersion7();

    public CreateProductCommandHandlerTests()
    {
        _unitOfMeasureRepository.SeedActiveStatus(ActiveUnitOfMeasureId, true);
        _handler = new CreateProductCommandHandler(
            _productRepository, _unitOfMeasureRepository, _unitOfWork);
    }

    private static CreateProductCommand ValidCommand(
        string? code = null,
        Guid? unitOfMeasureId = null) => new(
        Code: code ?? "PRD001",
        Name: "Test Product",
        UnitOfMeasureId: unitOfMeasureId ?? ActiveUnitOfMeasureId,
        ListPriceAmount: 100m,
        ListPriceCurrency: "TRY",
        ReorderPoint: 10m);

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldReturnProductId()
    {
        var command = ValidCommand();

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        _productRepository.Products.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateCode_ShouldReturnDuplicateCodeError()
    {
        Product product = Product.Create("PRD001", "Existing", ActiveUnitOfMeasureId,
            Money.Of(10m, Currency.TRY).Value, Quantity.Of(1m).Value).Value;
        _productRepository.Seed(product);

        var command = ValidCommand();

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.DuplicateCode);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentUnitOfMeasure_ShouldReturnUnitOfMeasureNotFoundError()
    {
        Guid nonExistentId = Guid.CreateVersion7();
        var command = ValidCommand(unitOfMeasureId: nonExistentId);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureNotFound);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUnitOfMeasure_ShouldReturnUnitOfMeasureInactiveError()
    {
        Guid inactiveId = Guid.CreateVersion7();
        _unitOfMeasureRepository.SeedActiveStatus(inactiveId, false);
        var command = ValidCommand(unitOfMeasureId: inactiveId);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureInactive);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCurrency_ShouldReturnCurrencyError()
    {
        var command = new CreateProductCommand(
            "PRD001", "Test", ActiveUnitOfMeasureId, 100m, "INVALID", 10m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CurrencyErrors.InvalidCode);
    }

    [Fact]
    public async Task HandleAsync_WhenUnitOfWorkThrowsDuplicateKey_ShouldReturnDuplicateCodeError()
    {
        _unitOfWork.ThrowOnSaveChanges(new DuplicateKeyException("IX_Products_Code"));
        var command = ValidCommand();

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.DuplicateCode);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSaveChanges()
    {
        var command = ValidCommand();

        await _handler.HandleAsync(command);

        _unitOfWork.SaveChangesCalled.ShouldBe(1);
    }
}
