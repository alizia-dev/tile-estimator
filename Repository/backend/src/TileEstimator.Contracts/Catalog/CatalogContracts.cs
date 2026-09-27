using System.ComponentModel.DataAnnotations;
using TileEstimator.Contracts.Common;

namespace TileEstimator.Contracts.Catalog;

public sealed record TileResponse(
    Guid Id,
    string Sku,
    string? Brand,
    string ProductName,
    string? Collection,
    string MaterialType,
    decimal LengthInches,
    decimal WidthInches,
    decimal? ThicknessInches,
    decimal CoverageSqFt,
    int TilesPerBox,
    decimal SqFtPerBox,
    decimal CostPerSqFt,
    decimal SellingPricePerSqFt,
    string? Finish,
    string? Color,
    string? Description,
    bool Active);

public sealed record SaveTileRequest
{
    [Required, MaxLength(64)]
    public string Sku { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProductName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Brand { get; init; }

    [MaxLength(100)]
    public string? Collection { get; init; }

    [Required]
    public string MaterialType { get; init; } = "Porcelain";

    [Range(0.01, 1000)]
    public decimal LengthInches { get; init; }

    [Range(0.01, 1000)]
    public decimal WidthInches { get; init; }

    [Range(0.01, 100)]
    public decimal? ThicknessInches { get; init; }

    [Range(1, 10000)]
    public int TilesPerBox { get; init; } = 1;

    /// <summary>Overrides the square feet per box derived from the face dimensions.</summary>
    [Range(0.0001, 100000)]
    public decimal? SqFtPerBoxOverride { get; init; }

    [Range(0, 100000)]
    public decimal CostPerSqFt { get; init; }

    [Range(0, 100000)]
    public decimal SellingPricePerSqFt { get; init; }

    [MaxLength(50)]
    public string? Finish { get; init; }

    [MaxLength(50)]
    public string? Color { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record MaterialResponse(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    string Unit,
    decimal? Coverage,
    decimal Cost,
    decimal SellingPrice,
    Guid? SupplierId,
    string? SupplierName,
    string? Description,
    bool Active);

public sealed record SaveMaterialRequest
{
    [Required, MaxLength(64)]
    public string Sku { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Category { get; init; } = "Other";

    [Required, MaxLength(16)]
    public string Unit { get; init; } = "EA";

    /// <summary>Square or linear feet one purchase unit covers. Null for counted items.</summary>
    [Range(0.0001, 1000000)]
    public decimal? Coverage { get; init; }

    [Range(0, 1000000)]
    public decimal Cost { get; init; }

    [Range(0, 1000000)]
    public decimal SellingPrice { get; init; }

    public Guid? SupplierId { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record PatternResponse(
    Guid Id, string Name, string? Description, decimal DefaultWastePercentage, bool Active, int SortOrder);

public sealed record SavePatternRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0, 200)]
    public decimal DefaultWastePercentage { get; init; }

    public bool Active { get; init; } = true;

    public int SortOrder { get; init; }
}

public sealed record WasteRuleResponse(
    Guid Id,
    string Name,
    Guid? PatternId,
    string? PatternName,
    string? SurfaceType,
    string? TileMaterialType,
    string? RoomType,
    decimal WastePercentage,
    int Priority,
    bool Active);

public sealed record SaveWasteRuleRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    public Guid? PatternId { get; init; }
    public string? SurfaceType { get; init; }
    public string? TileMaterialType { get; init; }
    public string? RoomType { get; init; }

    [Range(0, 200)]
    public decimal WastePercentage { get; init; }

    public int Priority { get; init; }
    public bool Active { get; init; } = true;
}

public sealed record LaborRateResponse(
    Guid Id,
    string Name,
    string Trade,
    string Unit,
    string CalculationMethod,
    decimal Rate,
    decimal? Productivity,
    string? Description,
    bool Active);

public sealed record SaveLaborRateRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Trade { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Unit { get; init; } = "SF";

    /// <summary>UnitRate or Productivity.</summary>
    [Required]
    public string CalculationMethod { get; init; } = "UnitRate";

    [Range(0, 100000)]
    public decimal Rate { get; init; }

    /// <summary>Units completed per hour. Required when the method is Productivity.</summary>
    [Range(0.0001, 100000)]
    public decimal? Productivity { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record AssemblyResponse(
    Guid Id,
    string Name,
    string? Description,
    string? AppliesToSurfaceType,
    bool Active,
    IReadOnlyList<AssemblyItemResponse> Items);

public sealed record AssemblyItemResponse(
    Guid Id,
    Guid? MaterialId,
    string? MaterialName,
    Guid? LaborRateId,
    string? LaborRateName,
    bool UsesSurfaceTile,
    string QuantityMethod,
    string? Unit,
    decimal Factor,
    decimal? FixedQuantity,
    decimal? CoverageOverride,
    decimal? WasteOverridePercentage,
    string? Description,
    int SortOrder);

public sealed record SaveAssemblyRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public string? AppliesToSurfaceType { get; init; }

    public bool Active { get; init; } = true;

    public IReadOnlyList<SaveAssemblyItemRequest> Items { get; init; } = [];
}

public sealed record SaveAssemblyItemRequest
{
    public Guid? MaterialId { get; init; }
    public Guid? LaborRateId { get; init; }
    public bool UsesSurfaceTile { get; init; }

    /// <summary>PerArea, PerLinearFoot, PerEach or PerCoverage.</summary>
    [Required]
    public string QuantityMethod { get; init; } = "PerArea";

    [MaxLength(16)]
    public string? Unit { get; init; }

    [Range(0.0001, 1000)]
    public decimal Factor { get; init; } = 1m;

    [Range(0.0001, 1000000)]
    public decimal? FixedQuantity { get; init; }

    [Range(0.0001, 1000000)]
    public decimal? CoverageOverride { get; init; }

    [Range(0, 200)]
    public decimal? WasteOverridePercentage { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }

    public int SortOrder { get; init; }
}

public sealed record SupplierResponse(
    Guid Id, string Name, string? ContactName, string? Email, string? Phone,
    string? AccountNumber, bool Active);

public sealed record SaveSupplierRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? ContactName { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(40)]
    public string? Phone { get; init; }

    [MaxLength(64)]
    public string? AccountNumber { get; init; }

    public AddressRequest? Address { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record AddressRequest
{
    [Required, MaxLength(200)]
    public string Line1 { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? Line2 { get; init; }

    [Required, MaxLength(100)]
    public string City { get; init; } = string.Empty;

    [Required, MaxLength(2), MinLength(2)]
    public string State { get; init; } = string.Empty;

    [Required, MaxLength(20)]
    public string PostalCode { get; init; } = string.Empty;

    [MaxLength(2)]
    public string Country { get; init; } = "US";
}

public sealed record AddressResponse(
    string Line1, string? Line2, string City, string State, string PostalCode, string Country, string SingleLine);

/// <summary>Result of a CSV import: what landed, and exactly which rows did not and why.</summary>
public sealed record ImportResultResponse(
    int TotalRows,
    int Imported,
    int Updated,
    int Failed,
    IReadOnlyList<ImportRowError> Errors);

public sealed record ImportRowError(int RowNumber, string? Sku, string Error);

public sealed record CatalogQuery : PagedRequest
{
    public bool? Active { get; init; }
    public string? Category { get; init; }
    public string? MaterialType { get; init; }
}
