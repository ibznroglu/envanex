using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.Warehouses;

public sealed class Warehouse : AggregateRoot<Guid>
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse() : base()
    {
        // EF Core
        Code = default!;
        Name = default!;
    }

    private Warehouse(Guid id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    public static Result<Warehouse> Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Warehouse>(WarehouseErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Warehouse>(WarehouseErrors.NameRequired);
        }

        string normalizedCode = code.Trim().ToUpperInvariant();
        string normalizedName = name.Trim();

        var warehouse = new Warehouse(Guid.CreateVersion7(), normalizedCode, normalizedName);
        return Result.Success(warehouse);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
