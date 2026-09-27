namespace TileEstimator.Domain.ValueObjects;

/// <summary>Units used by takeoff and costing. Stored as a string code on lines for auditability.</summary>
public static class UnitOfMeasure
{
    public const string SquareFeet = "SF";
    public const string LinearFeet = "LF";
    public const string Each = "EA";
    public const string Box = "BOX";
    public const string Bag = "BAG";
    public const string Sheet = "SHEET";
    public const string Gallon = "GAL";
    public const string Tube = "TUBE";
    public const string Roll = "ROLL";
    public const string Hour = "HR";
    public const string Piece = "PC";

    public static readonly IReadOnlyList<string> All =
    [
        SquareFeet, LinearFeet, Each, Box, Bag, Sheet, Gallon, Tube, Roll, Hour, Piece
    ];
}
