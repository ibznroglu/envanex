using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.DTOs;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeProductReadRepository : IProductReadRepository
{
    private readonly List<ProductDetailDto> _details = [];
    private readonly List<ProductListDto> _list = [];

    public void SeedDetail(ProductDetailDto dto)
    {
        _details.Add(dto);
    }

    public void SeedList(ProductListDto dto)
    {
        _list.Add(dto);
    }

    public IQueryable<ProductListDto> GetAll()
    {
        return _list.AsQueryable();
    }

    public Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ProductDetailDto? dto = _details.FirstOrDefault(d => d.Id == id);
        return Task.FromResult(dto);
    }
}
