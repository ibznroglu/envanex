using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.Warehouses;

public static class WarehouseErrors
{
    public static readonly Error CodeRequired = new("Warehouse.CodeRequired", "Warehouse code is required.");
    public static readonly Error NameRequired = new("Warehouse.NameRequired", "Warehouse name is required.");
}
