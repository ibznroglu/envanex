using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Application.Tests.Products.Commands;

public class ActivateProductCommandHandlerTests
{
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ActivateProductCommandHandler _handler;

    private static readonly Guid UnitOfMeasureId = Guid.CreateVersion7();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    public ActivateProductCommandHandlerTests()
    {
        _handler = new ActivateProductCommandHandler(_productRepository, _unitOfWork);
    }

    private Product SeedDeactivatedProduct()
    {
        Product product = Product.Create("PRD001", "Test Product", UnitOfMeasureId,
            Money.Of(50m, Currency.TRY).Value, Quantity.Of(5m).Value).Value;
        product.Deactivate();
        _productRepository.Seed(product);
        return product;
    }

    [Fact]
    public async Task HandleAsync_WithExistingProduct_ShouldActivateAndReturnProductId()
    {
        Product product = SeedDeactivatedProduct();
        var command = new ActivateProductCommand(product.Id, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(product.Id);
        product.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError()
    {
        var command = new ActivateProductCommand(Guid.CreateVersion7(), RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSetOriginalRowVersion()
    {
        Product product = SeedDeactivatedProduct();
        var command = new ActivateProductCommand(product.Id, RowVersion);

        await _handler.HandleAsync(command);

        _productRepository.OriginalRowVersions.ShouldContainKey(product.Id);
        _productRepository.OriginalRowVersions[product.Id].ShouldBe(RowVersion);
    }

    [Fact]
    public async Task HandleAsync_WhenUnitOfWorkThrowsConcurrencyConflict_ShouldReturnConcurrencyConflictError()
    {
        Product product = SeedDeactivatedProduct();
        _unitOfWork.ThrowOnSaveChanges(new ConcurrencyConflictException());
        var command = new ActivateProductCommand(product.Id, RowVersion);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.ConcurrencyConflict);
    }
}
