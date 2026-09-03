using Envanex.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Envanex.Infrastructure.Persistence.Conventions;

internal sealed class MoneyComplexTypeConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var complexProperty in modelBuilder.Metadata.GetEntityTypes()
            .SelectMany(e => e.GetComplexProperties())
            .Where(cp => cp.ComplexType.ClrType == typeof(Money)))
        {
            var amountProperty = complexProperty.ComplexType.FindProperty(nameof(Money.Amount));
            amountProperty?.SetPrecision(18);
            amountProperty?.SetScale(4);
        }
    }
}
