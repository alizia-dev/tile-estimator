using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.ValueObjects;

/// <summary>
/// A percentage stored the way a contractor types it: 12 means 12%.
/// <see cref="AsFraction"/> gives 0.12 for use in formulas.
/// </summary>
public readonly record struct Percentage
{
    public decimal Value { get; }

    public Percentage(decimal value)
    {
        DomainException.Require(value >= -100m, "Percentage cannot be below -100%.");
        Value = Rounding.Quantity(value);
    }

    public static readonly Percentage Zero = new(0m);

    public decimal AsFraction => Value / 100m;

    public static Percentage FromFraction(decimal fraction) => new(fraction * 100m);

    /// <summary>Applies the percentage as an increase: 100 at 12% becomes 112.</summary>
    public decimal Increase(decimal baseValue) => baseValue * (1m + AsFraction);

    public decimal Of(decimal baseValue) => baseValue * AsFraction;

    public override string ToString() => $"{Value:0.####}%";
}
