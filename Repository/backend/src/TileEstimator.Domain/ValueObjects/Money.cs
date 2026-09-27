using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.ValueObjects;

/// <summary>An amount of money in a specific currency. Always <see cref="decimal"/>, never double.</summary>
public readonly record struct Money
{
    public const string DefaultCurrency = "USD";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(currency), "Currency is required.");
        Amount = Rounding.Money(amount);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    /// <summary>Unrounded multiply then round once, so cost x quantity keeps its precision.</summary>
    public static Money FromRaw(decimal rawAmount, string currency = DefaultCurrency) => new(rawAmount, currency);

    public static Money operator +(Money a, Money b) { Assert(a, b); return new Money(a.Amount + b.Amount, a.Currency); }
    public static Money operator -(Money a, Money b) { Assert(a, b); return new Money(a.Amount - b.Amount, a.Currency); }
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);
    public static Money operator /(Money a, decimal divisor)
    {
        DomainException.Require(divisor != 0m, "Cannot divide money by zero.");
        return new Money(a.Amount / divisor, a.Currency);
    }

    public static bool operator >(Money a, Money b) { Assert(a, b); return a.Amount > b.Amount; }
    public static bool operator <(Money a, Money b) { Assert(a, b); return a.Amount < b.Amount; }
    public static bool operator >=(Money a, Money b) { Assert(a, b); return a.Amount >= b.Amount; }
    public static bool operator <=(Money a, Money b) { Assert(a, b); return a.Amount <= b.Amount; }

    private static void Assert(Money a, Money b) =>
        DomainException.Require(a.Currency == b.Currency, $"Currency mismatch: {a.Currency} vs {b.Currency}.");

    public override string ToString() => Currency == "USD"
        ? $"${Amount:N2}"
        : $"{Amount:N2} {Currency}";
}
