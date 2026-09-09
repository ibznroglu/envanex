using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.Products.Queries;

public class GetProductByIdQueryHandlerTests
{
    private readonly FakeProductReadRepository _readRepository = new();
    private readonly GetProductByIdQueryHandler _handler;

    public GetProductByIdQueryHandlerTests()
    {
        _handler = new GetProductByIdQueryHandler(_readRepository);
    }

    [Fact]
    public async Task HandleAsync_WithExistingProduct_ShouldReturnProductDetail()
    {
        Guid productId = Guid.CreateVersion7();
        var dto = new ProductDetailDto(
            productId, "PRD001", "Test Product", Guid.CreateVersion7(), "Adet",
            100m, "TRY", 10m, true, [1, 2, 3, 4, 5, 6, 7, 8]);
        _readRepository.SeedDetail(dto);

        var query = new GetProductByIdQuery(productId);

        Result<ProductDetailDto> result = await _handler.HandleAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError()
    {
        var query = new GetProductByIdQuery(Guid.CreateVersion7());

        Result<ProductDetailDto> result = await _handler.HandleAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NotFound);
    }
}
