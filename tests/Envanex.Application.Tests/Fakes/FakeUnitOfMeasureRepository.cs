using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.UnitOfMeasures;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeUnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly List<UnitOfMeasure> _units = [];
    private readonly Dictionary<Guid, bool> _activeStatuses = [];

    public IReadOnlyList<UnitOfMeasure> Units => _units.AsReadOnly();

    public void Seed(UnitOfMeasure unit)
    {
        _units.Add(unit);
    }

    /// <summary>
    /// Seeds the active status for a given unit of measure identifier.
    /// An unseeded identifier simulates a non-existent unit (<see langword="null"/> from
    /// <see cref="GetActiveStatusAsync"/>). Use <see langword="false"/> for inactive,
    /// <see langword="true"/> for active.
    /// </summary>
    public void SeedActiveStatus(Guid id, bool isActive)
    {
        _activeStatuses[id] = isActive;
    }

    public Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        UnitOfMeasure? unit = _units.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(unit);
    }

    public Task AddAsync(UnitOfMeasure unit, CancellationToken ct = default)
    {
        _units.Add(unit);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        bool exists = _units.Any(u =>
            string.Equals(u.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<bool?> GetActiveStatusAsync(Guid id, CancellationToken ct = default)
    {
        if (_activeStatuses.TryGetValue(id, out bool isActive))
        {
            return Task.FromResult<bool?>(isActive);
        }

        return Task.FromResult<bool?>(null);
    }
}
