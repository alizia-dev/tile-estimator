using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.ValueObjects;

/// <summary>A measured amount with its unit. Feeds money, so it is always decimal.</summary>
public readonly record struct Quantity
{
    public decimal Value { get; }
    public string Unit { get; }

    public Quantity(decimal value, string unit)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(unit), "Unit is required.");
        Value = Rounding.Quantity(value);
        Unit = unit;
    }

    public static Quantity Zero(string unit) => new(0m, unit);

    public static Quantity operator +(Quantity a, Quantity b)
    {
        DomainException.Require(a.Unit == b.Unit, $"Unit mismatch: {a.Unit} vs {b.Unit}.");
        return new Quantity(a.Value + b.Value, a.Unit);
    }

    public static Quantity operator *(Quantity a, decimal factor) => new(a.Value * factor, a.Unit);

    public Money Extend(Money unitPrice) => Money.FromRaw(unitPrice.Amount * Value, unitPrice.Currency);

    public override string ToString() => $"{Value:0.####} {Unit}";
}
