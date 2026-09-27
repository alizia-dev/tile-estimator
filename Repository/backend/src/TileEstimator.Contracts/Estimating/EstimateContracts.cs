using System.ComponentModel.DataAnnotations;
using TileEstimator.Contracts.Common;

namespace TileEstimator.Contracts.Estimating;

// --- Takeoff -------------------------------------------------------------------------------------

/// <summary>A calculated line, with the breakdown that explains where its number came from.</summary>
public sealed record TakeoffLineResponse(
    Guid? SurfaceId,
    Guid? RoomId,
    string Category,
    string Description,
    decimal Quantity,
    decimal PurchaseQuantity,
    string Unit,
    decimal WastePercentage,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalCost,
    decimal TotalPrice,
    string Calculation);

public sealed record SurfaceAreaResponse(
    Guid? SurfaceId,
    string SurfaceName,
    decimal GrossAreaSquareFeet,
    decimal OpeningDeductionSquareFeet,
    decimal OpeningAdditionSquareFeet,
    decimal NetAreaSquareFeet,
    decimal WastePercentage,
    string WasteSource,
    string WasteExplanation,
    decimal AdjustedAreaSquareFeet,
    string Calculation,
    IReadOnlyList<TakeoffLineResponse> Lines);

public sealed record TakeoffResponse(
    IReadOnlyList<SurfaceAreaResponse> Surfaces,
    IReadOnlyList<TakeoffLineResponse> Lines,
    decimal TotalNetAreaSquareFeet,
    decimal TotalAdjustedAreaSquareFeet,
    decimal TotalMaterialCost,
    decimal TotalLaborCost,
    decimal TotalCost,
    decimal TotalPrice);

// --- Quick calculators (SPEC 16) ------------------------------------------------------------------

/// <summary>Shared inputs for every quick calculator. All of them call the same engine.</summary>
public abstract record QuickCalculatorRequest
{
    public Guid? TileId { get; init; }
    public Guid? PatternId { get; init; }
    public Guid? AssemblyId { get; init; }

    [Range(0, 200)]
    public decimal? WasteOverridePercentage { get; init; }
}

public sealed record FloorCalculatorRequest : QuickCalculatorRequest
{
    [Range(0.01, 100000)]
    public decimal LengthFeet { get; init; }

    [Range(0.01, 100000)]
    public decimal WidthFeet { get; init; }

    [Range(0, 100000)]
    public decimal TrimLinearFeet { get; init; }

    public IReadOnlyList<QuickOpeningRequest> Openings { get; init; } = [];
}

public sealed record WallCalculatorRequest : QuickCalculatorRequest
{
    [Range(0.01, 100000)]
    public decimal LengthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal HeightFeet { get; init; }

    public IReadOnlyList<QuickOpeningRequest> Openings { get; init; } = [];
}

public sealed record BacksplashCalculatorRequest : QuickCalculatorRequest
{
    [Range(0.01, 100000)]
    public decimal LengthFeet { get; init; }

    [Range(0.01, 100)]
    public decimal HeightFeet { get; init; } = 1.5m;

    public IReadOnlyList<QuickOpeningRequest> Openings { get; init; } = [];
}

public sealed record ShowerCalculatorRequest
{
    [Range(0.01, 1000)]
    public decimal WidthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal DepthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal WallHeightFeet { get; init; } = 8m;

    public Guid? FloorTileId { get; init; }
    public Guid? WallTileId { get; init; }
    public Guid? FloorAssemblyId { get; init; }
    public Guid? WallAssemblyId { get; init; }
    public Guid? PatternId { get; init; }

    [Range(0, 200)]
    public decimal? WasteOverridePercentage { get; init; }

    public bool IncludeNiche { get; init; }

    [Range(0.01, 100)]
    public decimal NicheWidthFeet { get; init; } = 2m;

    [Range(0.01, 100)]
    public decimal NicheHeightFeet { get; init; } = 1.25m;

    public bool IncludeBench { get; init; }

    [Range(0.01, 100)]
    public decimal BenchWidthFeet { get; init; } = 3m;

    [Range(0.01, 100)]
    public decimal BenchDepthFeet { get; init; } = 1.5m;

    /// <summary>Door or entry opening deducted from the wall area.</summary>
    [Range(0, 100)]
    public decimal DoorWidthFeet { get; init; }

    [Range(0, 100)]
    public decimal DoorHeightFeet { get; init; }
}

public sealed record BathroomCalculatorRequest
{
    [Range(0.01, 1000)]
    public decimal FloorLengthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal FloorWidthFeet { get; init; }

    public Guid? FloorTileId { get; init; }
    public Guid? FloorAssemblyId { get; init; }

    /// <summary>Total run of tiled wall, if any.</summary>
    [Range(0, 10000)]
    public decimal WallLengthFeet { get; init; }

    [Range(0, 1000)]
    public decimal WallHeightFeet { get; init; }

    public Guid? WallTileId { get; init; }
    public Guid? WallAssemblyId { get; init; }

    public ShowerCalculatorRequest? Shower { get; init; }

    [Range(0, 1000)]
    public decimal BacksplashLengthFeet { get; init; }

    [Range(0, 100)]
    public decimal BacksplashHeightFeet { get; init; } = 1.5m;

    public Guid? BacksplashTileId { get; init; }
    public Guid? BacksplashAssemblyId { get; init; }

    public Guid? PatternId { get; init; }

    [Range(0, 200)]
    public decimal? WasteOverridePercentage { get; init; }
}

public sealed record QuickOpeningRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = "Opening";

    public string Type { get; init; } = "Door";

    [Range(0.01, 1000)]
    public decimal WidthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal HeightFeet { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; } = 1;

    public bool? AddsArea { get; init; }
}

// --- Estimates -----------------------------------------------------------------------------------

public sealed record EstimateResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string EstimateNumber,
    int Version,
    string DisplayNumber,
    Guid? SupersedesEstimateId,
    string Status,
    string? Title,
    string? Notes,
    string Currency,
    string PricingStrategy,
    decimal MarkupPercentage,
    decimal MarginPercentage,
    decimal FixedMarkupAmount,
    decimal OverheadPercentage,
    string DiscountType,
    decimal DiscountValue,
    decimal TaxRatePercentage,
    string TaxBasis,
    bool DiscountBeforeTax,
    decimal MaterialCost,
    decimal LaborCost,
    decimal OtherCost,
    decimal OverheadAmount,
    decimal TotalCost,
    decimal MarkupAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Subtotal,
    decimal GrandTotal,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    bool IsEditable,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion,
    IReadOnlyList<EstimateLineResponse> Lines);

public sealed record EstimateLineResponse(
    Guid Id,
    Guid? RoomId,
    string? RoomName,
    Guid? SurfaceId,
    string Category,
    string Description,
    decimal Quantity,
    decimal PurchaseQuantity,
    string Unit,
    decimal UnitCost,
    decimal UnitPrice,
    decimal WastePercentage,
    decimal TotalCost,
    decimal TotalPrice,
    string? Calculation,
    bool IsPriceOverridden,
    decimal? OriginalUnitPrice,
    string? OverrideReason,
    string? Notes,
    int SortOrder);

public sealed record CreateEstimateRequest
{
    [Required]
    public Guid ProjectId { get; init; }

    [MaxLength(200)]
    public string? Title { get; init; }
}

public sealed record UpdateEstimatePricingRequest
{
    /// <summary>Markup, Margin or FixedMarkup.</summary>
    [Required]
    public string PricingStrategy { get; init; } = "Markup";

    [Range(0, 10000)]
    public decimal MarkupPercentage { get; init; }

    [Range(0, 99.99)]
    public decimal MarginPercentage { get; init; }

    [Range(0, 10000000)]
    public decimal FixedMarkupAmount { get; init; }

    [Range(0, 1000)]
    public decimal OverheadPercentage { get; init; }

    /// <summary>None, Percentage or FixedAmount.</summary>
    public string DiscountType { get; init; } = "None";

    [Range(0, 10000000)]
    public decimal DiscountValue { get; init; }

    [Range(0, 100)]
    public decimal TaxRatePercentage { get; init; }

    /// <summary>MaterialsOnly, LaborOnly or MaterialsAndLabor.</summary>
    public string TaxBasis { get; init; } = "MaterialsAndLabor";

    public bool DiscountBeforeTax { get; init; } = true;

    /// <summary>Concurrency token from the estimate that was loaded (SPEC 18).</summary>
    public string? RowVersion { get; init; }
}

public sealed record SaveEstimateLineRequest
{
    /// <summary>Tile, Material, Labor, Equipment, Delivery or Other.</summary>
    [Required]
    public string Category { get; init; } = "Other";

    [Required, MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    [Range(0, 10000000)]
    public decimal Quantity { get; init; }

    [Required, MaxLength(16)]
    public string Unit { get; init; } = "EA";

    [Range(0, 10000000)]
    public decimal UnitCost { get; init; }

    [Range(0, 10000000)]
    public decimal UnitPrice { get; init; }

    public Guid? RoomId { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

public sealed record OverridePriceRequest
{
    [Range(0, 10000000)]
    public decimal UnitPrice { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}

public sealed record UpdateEstimateDetailsRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(4000)]
    public string? Notes { get; init; }
}

public sealed record EstimateQuery : PagedRequest
{
    public Guid? ProjectId { get; init; }
    public string? Status { get; init; }
}

public sealed record EstimateSummaryResponse(
    Guid Id,
    string EstimateNumber,
    int Version,
    string DisplayNumber,
    string Status,
    Guid ProjectId,
    string ProjectName,
    string CustomerName,
    decimal GrandTotal,
    string Currency,
    DateTime CreatedAt,
    DateTime? FinalizedAt);
