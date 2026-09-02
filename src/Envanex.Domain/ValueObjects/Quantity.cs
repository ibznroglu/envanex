using Envanex.Domain.Common;

namespace Envanex.Domain.ValueObjects;

public readonly record struct Quantity
{
    private const int DecimalPlaces = 6;

    public decimal Value { get; }

    private Quantity(decimal value)
    {
        Value = value;
    }

    public static Result<Quantity> Of(decimal value)
    {
        if (value < 0)
        {
            return Result.Failure<Quantity>(QuantityErrors.Negative);
        }

        decimal rounded = Math.Round(value, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Quantity(rounded));
    }

    public Quantity Add(Quantity other)
    {
        decimal sum = Math.Round(Value + other.Value, DecimalPlaces, MidpointRounding.ToEven);
        return new Quantity(sum);
    }

    public Result<Quantity> Subtract(Quantity other)
    {
        decimal difference = Value - other.Value;

        if (difference < 0)
        {
            return Result.Failure<Quantity>(QuantityErrors.NegativeResult);
        }

        decimal rounded = Math.Round(difference, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Quantity(rounded));
    }

    public Result<Quantity> Multiply(decimal factor)
    {
        decimal product = Value * factor;

        if (product < 0)
        {
            return Result.Failure<Quantity>(QuantityErrors.NegativeResult);
        }

        decimal rounded = Math.Round(product, DecimalPlaces, MidpointRounding.ToEven);
        return Result.Success(new Quantity(rounded));
    }

    public static readonly Quantity Zero = new(0m);
}

public static class QuantityErrors
{
    public static readonly Error Negative = new("Quantity.Negative", "Quantity cannot be negative.");
    public static readonly Error NegativeResult = new("Quantity.NegativeResult", "The operation would result in a negative quantity.");
}
