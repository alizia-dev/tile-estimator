using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 12 assembly: a complete installation system an organization configures once and reuses,
/// such as "Shower" = wall tile + floor tile + thinset + grout + backer board + waterproofing +
/// trim + caulk + labor. Applying an assembly to a surface expands it into estimate lines.
/// </summary>
public class Assembly : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Restricts the assembly to a surface type in the picker. Null means it suits any surface.</summary>
    public SurfaceType? AppliesToSurfaceType { get; private set; }

    public bool Active { get; private set; } = true;

    public ICollection<AssemblyItem> Items { get; private set; } = new List<AssemblyItem>();

    private Assembly() { }

    public static Assembly Create(Guid organizationId, string name, string? description,
        SurfaceType? appliesToSurfaceType)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Assembly name is required.");
        return new Assembly
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description,
            AppliesToSurfaceType = appliesToSurfaceType
        };
    }

    public void Update(string name, string? description, SurfaceType? appliesToSurfaceType, bool active)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Assembly name is required.");
        Name = name.Trim();
        Description = description;
        AppliesToSurfaceType = appliesToSurfaceType;
        Active = active;
    }
}

/// <summary>
/// SPEC 12 assembly item: one material or labor operation inside an assembly, together with the
/// method used to derive its quantity from the takeoff.
/// </summary>
public class AssemblyItem : TenantEntity
{
    public Guid AssemblyId { get; private set; }

    /// <summary>Set for a material line. Mutually exclusive with <see cref="LaborRateId"/>.</summary>
    public Guid? MaterialId { get; private set; }

    /// <summary>Set for a labor line. Mutually exclusive with <see cref="MaterialId"/>.</summary>
    public Guid? LaborRateId { get; private set; }

    /// <summary>True when this item consumes the tile selected on the surface rather than a catalog material.</summary>
    public bool UsesSurfaceTile { get; private set; }

    public QuantityMethod QuantityMethod { get; private set; } = QuantityMethod.PerArea;

    /// <summary>Multiplier applied to the derived quantity, e.g. 2 coats of membrane.</summary>
    public decimal Factor { get; private set; } = 1m;

    /// <summary>Fixed count for the PerEach method.</summary>
    public decimal? FixedQuantity { get; private set; }

    /// <summary>
    /// Coverage override for the PerCoverage method. When null the engine uses the material's own
    /// catalog coverage, so a price or coverage change in the catalog flows through to new estimates.
    /// </summary>
    public decimal? CoverageOverride { get; private set; }

    /// <summary>Applies this item's own waste instead of the surface waste. Null means use the surface waste.</summary>
    public decimal? WasteOverridePercentage { get; private set; }

    public string? Unit { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }

    public Assembly? Assembly { get; private set; }
    public Material? Material { get; private set; }
    public LaborRate? LaborRate { get; private set; }

    private AssemblyItem() { }

    public static AssemblyItem Create(Guid organizationId, Guid assemblyId, Guid? materialId, Guid? laborRateId,
        bool usesSurfaceTile, QuantityMethod quantityMethod, decimal factor, decimal? fixedQuantity,
        decimal? coverageOverride, string? unit, int sortOrder)
    {
        Validate(materialId, laborRateId, usesSurfaceTile, quantityMethod, factor, fixedQuantity);

        return new AssemblyItem
        {
            OrganizationId = organizationId,
            AssemblyId = assemblyId,
            MaterialId = materialId,
            LaborRateId = laborRateId,
            UsesSurfaceTile = usesSurfaceTile,
            QuantityMethod = quantityMethod,
            Factor = Rounding.Quantity(factor),
            FixedQuantity = fixedQuantity.HasValue ? Rounding.Quantity(fixedQuantity.Value) : null,
            CoverageOverride = coverageOverride.HasValue ? Rounding.Quantity(coverageOverride.Value) : null,
            Unit = unit?.Trim().ToUpperInvariant(),
            SortOrder = sortOrder
        };
    }

    public void Update(Guid? materialId, Guid? laborRateId, bool usesSurfaceTile, QuantityMethod quantityMethod,
        decimal factor, decimal? fixedQuantity, decimal? coverageOverride, decimal? wasteOverridePercentage,
        string? unit, string? description, int sortOrder)
    {
        Validate(materialId, laborRateId, usesSurfaceTile, quantityMethod, factor, fixedQuantity);

        MaterialId = materialId;
        LaborRateId = laborRateId;
        UsesSurfaceTile = usesSurfaceTile;
        QuantityMethod = quantityMethod;
        Factor = Rounding.Quantity(factor);
        FixedQuantity = fixedQuantity.HasValue ? Rounding.Quantity(fixedQuantity.Value) : null;
        CoverageOverride = coverageOverride.HasValue ? Rounding.Quantity(coverageOverride.Value) : null;
        WasteOverridePercentage = wasteOverridePercentage;
        Unit = unit?.Trim().ToUpperInvariant();
        Description = description;
        SortOrder = sortOrder;
    }

    public bool IsLabor => LaborRateId.HasValue;

    private static void Validate(Guid? materialId, Guid? laborRateId, bool usesSurfaceTile,
        QuantityMethod quantityMethod, decimal factor, decimal? fixedQuantity)
    {
        var targets = (materialId.HasValue ? 1 : 0) + (laborRateId.HasValue ? 1 : 0) + (usesSurfaceTile ? 1 : 0);
        DomainException.Require(targets == 1,
            "An assembly item must reference exactly one of: a material, a labor rate, or the surface tile.");
        DomainException.Require(factor > 0m, "Factor must be greater than zero.");
        DomainException.Require(
            quantityMethod != QuantityMethod.PerEach || fixedQuantity is > 0m,
            "The per-each method needs a fixed quantity greater than zero.");
    }
}
