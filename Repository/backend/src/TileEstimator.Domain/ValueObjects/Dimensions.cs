using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.ValueObjects;

/// <summary>A rectangular measurement in feet. Used for rooms, surfaces and openings.</summary>
public readonly record struct Dimensions
{
    public decimal LengthFeet { get; }
    public decimal WidthFeet { get; }

    public Dimensions(decimal lengthFeet, decimal widthFeet)
    {
        DomainException.Require(lengthFeet > 0m, "Length must be greater than zero.");
        DomainException.Require(widthFeet > 0m, "Width must be greater than zero.");
        LengthFeet = Rounding.Quantity(lengthFeet);
        WidthFeet = Rounding.Quantity(widthFeet);
    }

    /// <summary>Area in square feet. 12 x 15 = 180 SF (the §14 reference case).</summary>
    public decimal AreaSquareFeet => Rounding.Quantity(LengthFeet * WidthFeet);

    public decimal PerimeterFeet => Rounding.Quantity(2m * (LengthFeet + WidthFeet));

    public override string ToString() => $"{LengthFeet:0.##}' x {WidthFeet:0.##}'";
}
