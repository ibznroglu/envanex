using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence.Repositories;

internal sealed class UnitOfMeasureReadRepository : IUnitOfMeasureReadRepository
{
    private readonly EnvanexDbContext _context;

    public UnitOfMeasureReadRepository(EnvanexDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UnitOfMeasureListDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.UnitOfMeasures
            .AsNoTracking()
            .Select(u => new UnitOfMeasureListDto
            {
                Id = u.Id,
                Code = u.Code,
                Name = u.Name,
                IsActive = u.IsActive,
            })
            .ToListAsync(ct);
    }

    public async Task<UnitOfMeasureDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.UnitOfMeasures
            .AsNoTracking()
            .Where(u => u.Id == id)
            .GroupJoin(
                _context.UnitOfMeasures,
                u => u.BaseUnitId,
                b => b.Id,
                (u, bases) => new { Unit = u, Bases = bases })
            .SelectMany(
                x => x.Bases.DefaultIfEmpty(),
                (x, baseUnit) => new UnitOfMeasureDetailDto(
                    x.Unit.Id,
                    x.Unit.Code,
                    x.Unit.Name,
                    x.Unit.BaseUnitId,
                    baseUnit != null ? baseUnit.Name : null,
                    x.Unit.ConversionFactor,
                    x.Unit.IsActive,
                    EF.Property<byte[]>(x.Unit, ColumnNames.RowVersion)))
            .FirstOrDefaultAsync(ct);
    }
}
