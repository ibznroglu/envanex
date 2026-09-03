using Envanex.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Envanex.Infrastructure.Persistence.Conventions;

internal sealed class AggregateRootConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (!IsAggregateRoot(clrType))
            {
                continue;
            }

            if (entityType.FindProperty("RowVersion") is not null)
            {
                continue;
            }

            var property = entityType.AddProperty("RowVersion", typeof(byte[]));
            if (property is not null)
            {
                property.SetIsConcurrencyToken(true, fromDataAnnotation: false);
                property.SetValueGenerated(ValueGenerated.OnAddOrUpdate, fromDataAnnotation: false);
            }
        }
    }

    private static bool IsAggregateRoot(Type type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
