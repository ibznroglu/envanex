using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;
using Envanex.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly EnvanexDbContext _context;

    public ProductRepository(EnvanexDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _context.Products.AddAsync(product, ct);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Products.AnyAsync(p => p.Code == code, ct);
    }

    public void SetOriginalRowVersion(Product product, byte[] rowVersion)
    {
        _context.Entry(product).Property<byte[]>(ColumnNames.RowVersion).OriginalValue = rowVersion;
    }
}
