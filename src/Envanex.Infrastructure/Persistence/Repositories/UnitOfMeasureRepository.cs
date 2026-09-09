using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence.Repositories;

internal sealed class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly EnvanexDbContext _context;

    public UnitOfMeasureRepository(EnvanexDbContext context)
    {
        _context = context;
    }

    public async Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task AddAsync(UnitOfMeasure unit, CancellationToken ct = default)
    {
        await _context.UnitOfMeasures.AddAsync(unit, ct);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.UnitOfMeasures.AnyAsync(u => u.Code == code, ct);
    }

    public async Task<bool?> GetActiveStatusAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.UnitOfMeasures
            .Where(u => u.Id == id)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(ct);
    }
}
