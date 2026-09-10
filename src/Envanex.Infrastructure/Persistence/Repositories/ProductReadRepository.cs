using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.DTOs;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence.Repositories;

internal sealed class ProductReadRepository : IProductReadRepository
{
    private readonly EnvanexDbContext _context;

    public ProductReadRepository(EnvanexDbContext context)
    {
        _context = context;
    }

    public IQueryable<ProductListDto> GetAll()
    {
        return _context.Products
            .AsNoTracking()
            .Join(
                _context.UnitOfMeasures,
                p => p.UnitOfMeasureId,
                u => u.Id,
                (p, u) => new ProductListDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    UnitOfMeasureName = u.Name,
                    ListPriceAmount = p.ListPrice.Amount,
                    ListPriceCurrency = p.ListPrice.Currency.Code,
                    ReorderPoint = p.ReorderPoint.Value,
                    IsActive = p.IsActive,
                });
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Join(
                _context.UnitOfMeasures,
                p => p.UnitOfMeasureId,
                u => u.Id,
                (p, u) => new ProductDetailDto(
                    p.Id,
                    p.Code,
                    p.Name,
                    p.UnitOfMeasureId,
                    u.Name,
                    p.ListPrice.Amount,
                    p.ListPrice.Currency.Code,
                    p.ReorderPoint.Value,
                    p.IsActive,
                    EF.Property<byte[]>(p, ColumnNames.RowVersion)))
            .FirstOrDefaultAsync(ct);
    }
}
