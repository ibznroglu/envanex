using Envanex.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Envanex.Infrastructure.Persistence.Converters;

internal sealed class QuantityConverter : ValueConverter<Quantity, decimal>
{
    public QuantityConverter() : base(
        quantity => quantity.Value,
        value => Quantity.Of(value).Value)
    {
    }
}
