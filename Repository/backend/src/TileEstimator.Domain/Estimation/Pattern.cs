using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 11 layout pattern. Patterns are per-organization copies created from seed data at
/// registration, so a contractor can edit the default waste for a pattern without affecting
/// anyone else. There is no hard-coded industry-standard waste anywhere in the system.
/// </summary>
public class Pattern : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal DefaultWastePercentage { get; private set; }
    public bool Active { get; private set; } = true;
    public int SortOrder { get; private set; }

    private Pattern() { }

    public static Pattern Create(Guid organizationId, string name, string? description,
        decimal defaultWastePercentage, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Pattern name is required.");
        DomainException.Require(defaultWastePercentage >= 0m, "Default waste cannot be negative.");

        return new Pattern
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description,
            DefaultWastePercentage = Rounding.Quantity(defaultWastePercentage),
            SortOrder = sortOrder
        };
    }

    public void Update(string name, string? description, decimal defaultWastePercentage, bool active, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Pattern name is required.");
        DomainException.Require(defaultWastePercentage >= 0m, "Default waste cannot be negative.");

        Name = name.Trim();
        Description = description;
        DefaultWastePercentage = Rounding.Quantity(defaultWastePercentage);
        Active = active;
        SortOrder = sortOrder;
    }
}

/// <summary>
/// SPEC 11 waste rule. A rule matches on any combination of pattern, surface type, tile material
/// and room type; a null field means "any". The engine picks the most specific active match,
/// breaking ties on <see cref="Priority"/> (higher wins).
/// </summary>
public class WasteRule : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid? PatternId { get; private set; }
    public SurfaceType? SurfaceType { get; private set; }
    public TileMaterialType? TileMaterialType { get; private set; }
    public RoomType? RoomType { get; private set; }
    public decimal WastePercentage { get; private set; }
    public int Priority { get; private set; }
    public bool Active { get; private set; } = true;

    public Pattern? Pattern { get; private set; }

    private WasteRule() { }

    public static WasteRule Create(Guid organizationId, string name, decimal wastePercentage, int priority,
        Guid? patternId, SurfaceType? surfaceType, TileMaterialType? tileMaterialType, RoomType? roomType)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Waste rule name is required.");
        DomainException.Require(wastePercentage >= 0m, "Waste cannot be negative.");

        return new WasteRule
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            WastePercentage = Rounding.Quantity(wastePercentage),
            Priority = priority,
            PatternId = patternId,
            SurfaceType = surfaceType,
            TileMaterialType = tileMaterialType,
            RoomType = roomType
        };
    }

    public void Update(string name, decimal wastePercentage, int priority, Guid? patternId,
        SurfaceType? surfaceType, TileMaterialType? tileMaterialType, RoomType? roomType, bool active)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Waste rule name is required.");
        DomainException.Require(wastePercentage >= 0m, "Waste cannot be negative.");

        Name = name.Trim();
        WastePercentage = Rounding.Quantity(wastePercentage);
        Priority = priority;
        PatternId = patternId;
        SurfaceType = surfaceType;
        TileMaterialType = tileMaterialType;
        RoomType = roomType;
        Active = active;
    }

    /// <summary>How many fields this rule pins down. Used to prefer the most specific match.</summary>
    public int Specificity =>
        (PatternId.HasValue ? 1 : 0) +
        (SurfaceType.HasValue ? 1 : 0) +
        (TileMaterialType.HasValue ? 1 : 0) +
        (RoomType.HasValue ? 1 : 0);
}
