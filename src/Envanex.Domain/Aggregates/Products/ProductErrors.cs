using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.Products;

public static class ProductErrors
{
    public static readonly Error CodeRequired = new("Product.CodeRequired", "Product code is required.");
    public static readonly Error NameRequired = new("Product.NameRequired", "Product name is required.");
    public static readonly Error UnitOfMeasureRequired = new("Product.UnitOfMeasureRequired", "Unit of measure identifier must not be an empty GUID.");
    public static readonly Error CodeTooLong = new("Product.CodeTooLong", "Product code must not exceed 50 characters.");
    public static readonly Error NameTooLong = new("Product.NameTooLong", "Product name must not exceed 200 characters.");
    public static readonly Error NotFound = new("Product.NotFound", "Product was not found.");
    public static readonly Error ConcurrencyConflict = new("Product.ConcurrencyConflict", "The record has been modified by another user.");
    public static readonly Error DuplicateCode = new("Product.DuplicateCode", "A product with this code already exists.");
    public static readonly Error UnitOfMeasureNotFound = new("Product.UnitOfMeasureNotFound", "The specified unit of measure was not found.");
    public static readonly Error UnitOfMeasureInactive = new("Product.UnitOfMeasureInactive", "Cannot assign an inactive unit of measure.");
    public static readonly Error IdRequired = new("Product.IdRequired", "Product identifier is required.");
    public static readonly Error RowVersionRequired = new("Product.RowVersionRequired", "Row version is required for concurrency control.");
    public static readonly Error ListPriceAmountNegative = new("Product.ListPriceAmountNegative", "List price amount must not be negative.");
    public static readonly Error ReorderPointNegative = new("Product.ReorderPointNegative", "Reorder point must not be negative.");
    public static readonly Error ListPriceCurrencyRequired = new("Product.ListPriceCurrencyRequired", "List price currency is required.");
}
