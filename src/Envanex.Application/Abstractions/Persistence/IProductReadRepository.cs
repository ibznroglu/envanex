using Envanex.Application.Products.DTOs;

namespace Envanex.Application.Abstractions.Persistence;

public interface IProductReadRepository
{
    IQueryable<ProductListDto> GetAll();
    Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
