using Envanex.Domain.Aggregates.UnitOfMeasures;

namespace Envanex.Application.Abstractions.Persistence;

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(UnitOfMeasure unit, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<bool?> GetActiveStatusAsync(Guid id, CancellationToken ct = default);
}
