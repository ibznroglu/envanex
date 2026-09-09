using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeProductRepository : IProductRepository
{
    private readonly List<Product> _products = [];
    private readonly Dictionary<Guid, byte[]> _originalRowVersions = [];

    public IReadOnlyList<Product> Products => _products.AsReadOnly();
    public IReadOnlyDictionary<Guid, byte[]> OriginalRowVersions => _originalRowVersions;

    /// <summary>
    /// Number of times <see cref="SetOriginalRowVersion"/> has been called.
    /// Used by <see cref="FakeUnitOfWork"/> to verify call ordering.
    /// </summary>
    public int SetOriginalRowVersionCallCount { get; private set; }

    public void Seed(Product product)
    {
        _products.Add(product);
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Product? product = _products.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(product);
    }

    public Task AddAsync(Product product, CancellationToken ct = default)
    {
        _products.Add(product);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        bool exists = _products.Any(p =>
            string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public void SetOriginalRowVersion(Product product, byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(product);
        _originalRowVersions[product.Id] = rowVersion;
        SetOriginalRowVersionCallCount++;
    }
}
