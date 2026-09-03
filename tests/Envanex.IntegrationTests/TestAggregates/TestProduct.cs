using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;

namespace Envanex.IntegrationTests.TestAggregates;

public sealed class TestProduct : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public Money UnitPrice { get; private set; }
    public Quantity StockQuantity { get; private set; }

    private TestProduct() : base()
    {
        Name = default!;
        UnitPrice = default!;
    }

    private TestProduct(Guid id, string name, Money unitPrice, Quantity stockQuantity) : base(id)
    {
        Name = name;
        UnitPrice = unitPrice;
        StockQuantity = stockQuantity;
    }

    public static TestProduct Create(string name, Money unitPrice, Quantity stockQuantity)
    {
        return new TestProduct(Guid.NewGuid(), name, unitPrice, stockQuantity);
    }

    public void UpdatePrice(Money newPrice)
    {
        UnitPrice = newPrice;
    }
}
