using Envanex.Application.UnitOfMeasures.DTOs;

namespace Envanex.Application.Abstractions.Persistence;

public interface IUnitOfMeasureReadRepository
{
    Task<IReadOnlyList<UnitOfMeasureListDto>> GetAllAsync(CancellationToken ct = default);
    Task<UnitOfMeasureDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
