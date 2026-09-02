using Envanex.Domain.Common;

namespace Envanex.Domain.ValueObjects;

public sealed record Currency
{
    private static readonly HashSet<string> SupportedCodes = ["TRY", "USD", "EUR"];

    public string Code { get; }

    private Currency(string code)
    {
        Code = code;
    }

    public static Result<Currency> Of(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Currency>(CurrencyErrors.InvalidCode);
        }

        string normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || !SupportedCodes.Contains(normalized))
        {
            return Result.Failure<Currency>(CurrencyErrors.InvalidCode);
        }

        return Result.Success(new Currency(normalized));
    }

    public static readonly Currency TRY = new("TRY");
    public static readonly Currency USD = new("USD");
    public static readonly Currency EUR = new("EUR");
}

public static class CurrencyErrors
{
    public static readonly Error InvalidCode = new("Currency.InvalidCode", "The currency code is not a valid ISO 4217 code.");
}
