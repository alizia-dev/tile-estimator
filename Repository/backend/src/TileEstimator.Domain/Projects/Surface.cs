using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Projects;

/// <summary>
/// SPEC 9 surface (zone): the tiled plane inside a room. A room has many surfaces, and each
/// surface is measured according to its type:
/// <list type="bullet">
///   <item>Floor / Shower Floor: Length x Width</item>
///   <item>Wall / Shower Wall / Backsplash: Length x Height, one surface per wall run</item>
/// </list>
/// Irregular shapes use <see cref="AreaOverrideSquareFeet"/>, which wins over the dimensions.
/// </summary>
public class Surface : TenantEntity
{
    public Guid RoomId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SurfaceType Type { get; private set; }

    public decimal LengthFeet { get; private set; }

    /// <summary>Width in feet. Required for floor-type surfaces.</summary>
    public decimal? WidthFeet { get; private set; }

    /// <summary>Height in feet. Required for wall-type surfaces.</summary>
    public decimal? HeightFeet { get; private set; }

    /// <summary>Directly entered area for shapes the length/width/height model cannot express.</summary>
    public decimal? AreaOverrideSquareFeet { get; private set; }

    /// <summary>Trim, bullnose and base are bought by the running foot, not by area.</summary>
    public decimal TrimLinearFeet { get; private set; }

    /// <summary>Selected tile for this surface. Null until the estimator picks one.</summary>
    public Guid? TileId { get; private set; }

    /// <summary>Selected layout pattern, which supplies the default waste percentage.</summary>
    public Guid? PatternId { get; private set; }

    /// <summary>Assembly used to expand this surface into materials and labor.</summary>
    public Guid? AssemblyId { get; private set; }

    /// <summary>Estimator's explicit waste override. Wins over the pattern default and waste rules.</summary>
    public decimal? WasteOverridePercentage { get; private set; }

    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }

    public Room? Room { get; private set; }
    public ICollection<Opening> Openings { get; private set; } = new List<Opening>();

    private Surface() { }

    public static Surface Create(Guid organizationId, Guid roomId, string name, SurfaceType type,
        decimal lengthFeet, decimal? widthFeet, decimal? heightFeet, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Surface name is required.");
        Validate(type, lengthFeet, widthFeet, heightFeet);

        return new Surface
        {
            OrganizationId = organizationId,
            RoomId = roomId,
            Name = name.Trim(),
            Type = type,
            LengthFeet = Rounding.Quantity(lengthFeet),
            WidthFeet = widthFeet.HasValue ? Rounding.Quantity(widthFeet.Value) : null,
            HeightFeet = heightFeet.HasValue ? Rounding.Quantity(heightFeet.Value) : null,
            SortOrder = sortOrder
        };
    }

    public void UpdateDimensions(decimal lengthFeet, decimal? widthFeet, decimal? heightFeet,
        decimal? areaOverrideSquareFeet, decimal trimLinearFeet)
    {
        Validate(Type, lengthFeet, widthFeet, heightFeet);
        DomainException.Require(areaOverrideSquareFeet is null or > 0m, "An area override must be greater than zero.");
        DomainException.Require(trimLinearFeet >= 0m, "Trim linear feet cannot be negative.");

        LengthFeet = Rounding.Quantity(lengthFeet);
        WidthFeet = widthFeet.HasValue ? Rounding.Quantity(widthFeet.Value) : null;
        HeightFeet = heightFeet.HasValue ? Rounding.Quantity(heightFeet.Value) : null;
        AreaOverrideSquareFeet = areaOverrideSquareFeet.HasValue ? Rounding.Quantity(areaOverrideSquareFeet.Value) : null;
        TrimLinearFeet = Rounding.Quantity(trimLinearFeet);
    }

    public void UpdateSelections(Guid? tileId, Guid? patternId, Guid? assemblyId, decimal? wasteOverridePercentage)
    {
        DomainException.Require(wasteOverridePercentage is null or >= 0m, "Waste override cannot be negative.");
        TileId = tileId;
        PatternId = patternId;
        AssemblyId = assemblyId;
        WasteOverridePercentage = wasteOverridePercentage;
    }

    public void UpdateDetails(string name, string? notes, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Surface name is required.");
        Name = name.Trim();
        Notes = notes;
        SortOrder = sortOrder;
    }

    /// <summary>True for surfaces measured Length x Width rather than Length x Height.</summary>
    public bool IsFloorType => Type is SurfaceType.Floor or SurfaceType.ShowerFloor;

    private static void Validate(SurfaceType type, decimal lengthFeet, decimal? widthFeet, decimal? heightFeet)
    {
        DomainException.Require(lengthFeet > 0m, "Length must be greater than zero.");

        if (type is SurfaceType.Floor or SurfaceType.ShowerFloor)
        {
            DomainException.Require(widthFeet is > 0m, "A floor surface needs a width greater than zero.");
        }
        else if (type is SurfaceType.Wall or SurfaceType.ShowerWall or SurfaceType.Backsplash)
        {
            DomainException.Require(heightFeet is > 0m, "A wall surface needs a height greater than zero.");
        }
        else
        {
            DomainException.Require(widthFeet is > 0m || heightFeet is > 0m,
                "A custom surface needs either a width or a height.");
        }
    }
}

/// <summary>
/// SPEC 9 opening: a door, window, niche or bench subtracted from (or added to) a surface.
/// Quantity lets one row cover several identical openings.
/// </summary>
public class Opening : TenantEntity
{
    public Guid SurfaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public OpeningType Type { get; private set; }
    public decimal WidthFeet { get; private set; }
    public decimal HeightFeet { get; private set; }
    public int Quantity { get; private set; } = 1;

    /// <summary>
    /// A niche or bench is tiled, so its area is added rather than deducted. Doors and windows deduct.
    /// </summary>
    public bool AddsArea { get; private set; }

    public Surface? Surface { get; private set; }

    private Opening() { }

    public static Opening Create(Guid organizationId, Guid surfaceId, string name, OpeningType type,
        decimal widthFeet, decimal heightFeet, int quantity)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Opening name is required.");
        DomainException.Require(widthFeet > 0m, "Opening width must be greater than zero.");
        DomainException.Require(heightFeet > 0m, "Opening height must be greater than zero.");
        DomainException.Require(quantity > 0, "Opening quantity must be at least one.");

        return new Opening
        {
            OrganizationId = organizationId,
            SurfaceId = surfaceId,
            Name = name.Trim(),
            Type = type,
            WidthFeet = Rounding.Quantity(widthFeet),
            HeightFeet = Rounding.Quantity(heightFeet),
            Quantity = quantity,
            AddsArea = type is OpeningType.Niche or OpeningType.Bench
        };
    }

    public void Update(string name, OpeningType type, decimal widthFeet, decimal heightFeet, int quantity, bool addsArea)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Opening name is required.");
        DomainException.Require(widthFeet > 0m, "Opening width must be greater than zero.");
        DomainException.Require(heightFeet > 0m, "Opening height must be greater than zero.");
        DomainException.Require(quantity > 0, "Opening quantity must be at least one.");

        Name = name.Trim();
        Type = type;
        WidthFeet = Rounding.Quantity(widthFeet);
        HeightFeet = Rounding.Quantity(heightFeet);
        Quantity = quantity;
        AddsArea = addsArea;
    }

    /// <summary>Total square feet this row represents across all its copies.</summary>
    public decimal TotalAreaSquareFeet => Rounding.Quantity(WidthFeet * HeightFeet * Quantity);
}
