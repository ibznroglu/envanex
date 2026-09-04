using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.Products;

public static class ProductErrors
{
    public static readonly Error CodeRequired = new("Product.CodeRequired", "Product code is required.");
    public static readonly Error NameRequired = new("Product.NameRequired", "Product name is required.");
    public static readonly Error UnitOfMeasureRequired = new("Product.UnitOfMeasureRequired", "Unit of measure identifier must not be an empty GUID.");
}
