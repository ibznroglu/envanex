using Envanex.Domain.Common;

namespace Envanex.Domain.ValueObjects;

public sealed record Money
{
    private const int DecimalPlaces = 4;

    public decimal Amount { get; private init; }
    public Currency Currency { get; private init; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Of(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        decimal rounded = Math.Round(amount, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Money(rounded, currency));
    }

    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Currency != other.Currency)
        {
            return Result.Failure<Money>(MoneyErrors.CurrencyMismatch);
        }

        decimal sum = Math.Round(Amount + other.Amount, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Money(sum, Currency));
    }

    public Result<Money> Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Currency != other.Currency)
        {
            return Result.Failure<Money>(MoneyErrors.CurrencyMismatch);
        }

        decimal difference = Math.Round(Amount - other.Amount, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Money(difference, Currency));
    }

    public Money Multiply(decimal factor)
    {
        decimal product = Math.Round(Amount * factor, DecimalPlaces, MidpointRounding.ToEven);
        return new Money(product, Currency);
    }

    public static Money Zero(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(0m, currency);
    }
}

public static class MoneyErrors
{
    public static readonly Error CurrencyMismatch = new("Money.CurrencyMismatch", "Cannot perform arithmetic on Money with different currencies.");
}
