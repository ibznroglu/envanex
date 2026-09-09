using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.DTOs;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;

namespace Envanex.Application.Products.Queries;

public sealed class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IProductReadRepository _productReadRepository;

    public GetProductByIdQueryHandler(IProductReadRepository productReadRepository)
    {
        _productReadRepository = productReadRepository;
    }

    public async Task<Result<ProductDetailDto>> HandleAsync(GetProductByIdQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ProductDetailDto? dto = await _productReadRepository.GetByIdAsync(query.Id, ct);

        if (dto is null)
        {
            return Result.Failure<ProductDetailDto>(ProductErrors.NotFound);
        }

        return Result.Success(dto);
    }
}
