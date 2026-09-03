using Envanex.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Envanex.Infrastructure.Persistence.Converters;

internal sealed class CurrencyConverter : ValueConverter<Currency, string>
{
    public CurrencyConverter() : base(
        currency => currency.Code,
        code => Currency.Of(code).Value)
    {
    }
}
