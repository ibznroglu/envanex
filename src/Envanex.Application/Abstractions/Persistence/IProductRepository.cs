using Envanex.Domain.Aggregates.Products;

namespace Envanex.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    void SetOriginalRowVersion(Product product, byte[] rowVersion);
}
