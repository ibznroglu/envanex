using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.UnitOfMeasures.DTOs;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeUnitOfMeasureReadRepository : IUnitOfMeasureReadRepository
{
    private readonly List<UnitOfMeasureDetailDto> _details = [];
    private readonly List<UnitOfMeasureListDto> _list = [];

    public void SeedDetail(UnitOfMeasureDetailDto dto)
    {
        _details.Add(dto);
    }

    public void SeedList(UnitOfMeasureListDto dto)
    {
        _list.Add(dto);
    }

    public Task<IReadOnlyList<UnitOfMeasureListDto>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<UnitOfMeasureListDto>>(_list.AsReadOnly());
    }

    public Task<UnitOfMeasureDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        UnitOfMeasureDetailDto? dto = _details.FirstOrDefault(d => d.Id == id);
        return Task.FromResult(dto);
    }
}
