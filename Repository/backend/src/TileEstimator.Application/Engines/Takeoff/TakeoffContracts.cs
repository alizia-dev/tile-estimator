using TileEstimator.Domain.Enums;

namespace TileEstimator.Application.Engines.Takeoff;

/// <summary>An opening on a surface, as the engine sees it. Deductions and additions are both here.</summary>
public sealed record OpeningInput(
    string Name,
    OpeningType Type,
    decimal WidthFeet,
    decimal HeightFeet,
    int Quantity,
    bool AddsArea);

/// <summary>
/// The tile selected for a surface, reduced to the three numbers the takeoff needs.
/// Coverage always comes from the organization's catalog, never from a constant in code.
/// </summary>
public sealed record TileInput(
    Guid TileId,
    string Description,
    decimal CoverageSqFtPerTile,
    decimal SqFtPerBox,
    decimal CostPerSqFt,
    decimal SellingPricePerSqFt);

/// <summary>
/// A material or labor operation to expand for a surface, taken from an assembly item plus the
/// catalog row it points at. <see cref="Coverage"/> is how much one purchase unit covers.
/// </summary>
public sealed record AssemblyItemInput(
    Guid AssemblyItemId,
    Guid? MaterialId,
    Guid? LaborRateId,
    bool UsesSurfaceTile,
    string Description,
    QuantityMethod QuantityMethod,
    string Unit,
    decimal Factor,
    decimal? FixedQuantity,
    decimal? Coverage,
    decimal? WasteOverridePercentage,
    decimal UnitCost,
    decimal UnitPrice,
    LaborCalculationMethod? LaborMethod = null,
    decimal? LaborProductivity = null,
    int SortOrder = 0);

/// <summary>
/// The waste inputs in the order SPEC 11 resolves them:
/// pattern default, then the best matching waste rule, then the estimator's override.
/// </summary>
public sealed record WasteInput(
    decimal? PatternDefaultPercentage,
    decimal? MatchedRulePercentage,
    decimal? EstimatorOverridePercentage,
    string? PatternName = null,
    string? MatchedRuleName = null);

/// <summary>Where the final waste percentage came from. Shown to the estimator so it is never a mystery.</summary>
public enum WasteSource { None = 0, PatternDefault = 1, WasteRule = 2, EstimatorOverride = 3 }

public sealed record WasteResolution(decimal Percentage, WasteSource Source, string Explanation);

/// <summary>One surface to take off: its measurements, its openings and what goes on it.</summary>
public sealed record SurfaceTakeoffInput
{
    public Guid? SurfaceId { get; init; }
    public Guid? RoomId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? RoomName { get; init; }
    public required SurfaceType SurfaceType { get; init; }

    public decimal LengthFeet { get; init; }
    public decimal? WidthFeet { get; init; }
    public decimal? HeightFeet { get; init; }

    /// <summary>Directly entered area. When set it replaces the length/width/height calculation.</summary>
    public decimal? AreaOverrideSquareFeet { get; init; }

    public decimal TrimLinearFeet { get; init; }

    public IReadOnlyList<OpeningInput> Openings { get; init; } = [];
    public TileInput? Tile { get; init; }
    public required WasteInput Waste { get; init; }
    public IReadOnlyList<AssemblyItemInput> AssemblyItems { get; init; } = [];
    public Guid? AssemblyId { get; init; }
}

/// <summary>The area figures for one surface, before any material is derived from them.</summary>
public sealed record AreaResult(
    decimal GrossAreaSquareFeet,
    decimal OpeningDeductionSquareFeet,
    decimal OpeningAdditionSquareFeet,
    decimal NetAreaSquareFeet,
    decimal WastePercentage,
    decimal AdjustedAreaSquareFeet,
    WasteResolution Waste,
    CalculationBreakdown Breakdown);

/// <summary>A quantified line produced by the takeoff: tile, material or labor.</summary>
public sealed record TakeoffLineResult
{
    public Guid? SurfaceId { get; init; }
    public Guid? RoomId { get; init; }
    public required EstimateLineCategory Category { get; init; }
    public required string Description { get; init; }

    /// <summary>The exact calculated amount, e.g. 20.16 boxes.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>What must be bought, e.g. 21 boxes. Same as Quantity when nothing is packaged.</summary>
    public required decimal PurchaseQuantity { get; init; }

    public required string Unit { get; init; }
    public decimal WastePercentage { get; init; }
    public decimal UnitCost { get; init; }
    public decimal UnitPrice { get; init; }
    public Guid? TileId { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? LaborRateId { get; init; }
    public Guid? AssemblyId { get; init; }
    public decimal? NetAreaSquareFeet { get; init; }
    public decimal? AdjustedAreaSquareFeet { get; init; }
    public required CalculationBreakdown Breakdown { get; init; }
    public int SortOrder { get; init; }

    /// <summary>Extended cost. Uses the purchase quantity, because that is what gets paid for.</summary>
    public decimal TotalCost => Domain.ValueObjects.Rounding.Money(UnitCost * BillableQuantity);

    public decimal TotalPrice => Domain.ValueObjects.Rounding.Money(UnitPrice * BillableQuantity);

    private decimal BillableQuantity => PurchaseQuantity > 0m ? PurchaseQuantity : Quantity;
}

/// <summary>Everything the takeoff produced for one surface.</summary>
public sealed record SurfaceTakeoffResult(
    Guid? SurfaceId,
    string SurfaceName,
    AreaResult Area,
    IReadOnlyList<TakeoffLineResult> Lines);

/// <summary>The takeoff for a whole project or quick calculator run.</summary>
public sealed record TakeoffResult(
    IReadOnlyList<SurfaceTakeoffResult> Surfaces,
    IReadOnlyList<TakeoffLineResult> Lines,
    decimal TotalNetAreaSquareFeet,
    decimal TotalAdjustedAreaSquareFeet);

/// <summary>Tile counts for a surface: whole tiles and whole boxes.</summary>
public sealed record TileQuantityResult(
    decimal TilesCalculated,
    int TilesToPurchase,
    decimal BoxesCalculated,
    int BoxesToPurchase,
    CalculationBreakdown Breakdown);
