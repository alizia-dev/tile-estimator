namespace TileEstimator.Domain.ValueObjects;

/// <summary>
/// The single rounding policy for the whole system (docs/estimation-engine.md).
/// Money: 2 dp, AwayFromZero. Quantities/rates/percentages: 4 dp, AwayFromZero.
/// Purchase quantities: ceiling to whole units. Rounding happens at the boundary of a
/// calculation step, never mid-expression.
/// </summary>
public static class Rounding
{
    public const int MoneyDecimals = 2;
    public const int QuantityDecimals = 4;
    public const MidpointRounding Mode = MidpointRounding.AwayFromZero;

    public static decimal Money(decimal value) => decimal.Round(value, MoneyDecimals, Mode);

    public static decimal Quantity(decimal value) => decimal.Round(value, QuantityDecimals, Mode);

    /// <summary>Purchase quantity: you cannot buy 20.16 boxes, you buy 21.</summary>
    public static int PurchaseUnits(decimal calculatedQuantity) =>
        calculatedQuantity <= 0m ? 0 : (int)Math.Ceiling(calculatedQuantity);
}
