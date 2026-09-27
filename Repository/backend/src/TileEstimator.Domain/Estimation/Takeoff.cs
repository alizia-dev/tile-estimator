using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 14 takeoff: the persisted answer to "how much material does this project need?".
/// A takeoff holds quantities only. It knows nothing about markup, margin or tax.
/// </summary>
public class Takeoff : TenantEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CalculatedAt { get; private set; }
    public decimal TotalNetAreaSquareFeet { get; private set; }
    public decimal TotalAdjustedAreaSquareFeet { get; private set; }
    public string? Notes { get; private set; }

    public Project? Project { get; private set; }
    public ICollection<TakeoffItem> Items { get; private set; } = new List<TakeoffItem>();

    private Takeoff() { }

    public static Takeoff Create(Guid organizationId, Guid projectId, string name, DateTime utcNow)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Takeoff name is required.");
        return new Takeoff
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Name = name.Trim(),
            CalculatedAt = utcNow
        };
    }

    public void ApplyTotals(decimal netArea, decimal adjustedArea, DateTime utcNow)
    {
        TotalNetAreaSquareFeet = Rounding.Quantity(netArea);
        TotalAdjustedAreaSquareFeet = Rounding.Quantity(adjustedArea);
        CalculatedAt = utcNow;
    }

    public void ReplaceItems(IEnumerable<TakeoffItem> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public void SetNotes(string? notes) => Notes = notes;
}

/// <summary>
/// One quantified row of a takeoff: a tile, a material or a labor operation, with both the
/// calculated quantity and the quantity that must actually be purchased.
/// </summary>
public class TakeoffItem : TenantEntity
{
    public Guid TakeoffId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? SurfaceId { get; private set; }

    public EstimateLineCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }
    public decimal PurchaseQuantity { get; private set; }
    public string Unit { get; private set; } = UnitOfMeasure.Each;

    public decimal WastePercentage { get; private set; }
    public decimal? NetAreaSquareFeet { get; private set; }
    public decimal? AdjustedAreaSquareFeet { get; private set; }

    public Guid? TileId { get; private set; }
    public Guid? MaterialId { get; private set; }
    public Guid? LaborRateId { get; private set; }
    public Guid? AssemblyId { get; private set; }

    public string? CalculationReference { get; private set; }
    public int SortOrder { get; private set; }

    public Takeoff? Takeoff { get; private set; }

    private TakeoffItem() { }

    public static TakeoffItem Create(Guid organizationId, Guid takeoffId, EstimateLineCategory category,
        string description, decimal quantity, decimal purchaseQuantity, string unit, decimal wastePercentage,
        string? calculationReference, int sortOrder) => new()
        {
            OrganizationId = organizationId,
            TakeoffId = takeoffId,
            Category = category,
            Description = description,
            Quantity = Rounding.Quantity(quantity),
            PurchaseQuantity = Rounding.Quantity(purchaseQuantity),
            Unit = unit,
            WastePercentage = Rounding.Quantity(wastePercentage),
            CalculationReference = calculationReference,
            SortOrder = sortOrder
        };

    public void LinkSources(Guid? roomId, Guid? surfaceId, Guid? tileId, Guid? materialId,
        Guid? laborRateId, Guid? assemblyId)
    {
        RoomId = roomId;
        SurfaceId = surfaceId;
        TileId = tileId;
        MaterialId = materialId;
        LaborRateId = laborRateId;
        AssemblyId = assemblyId;
    }

    public void SetAreas(decimal? netArea, decimal? adjustedArea)
    {
        NetAreaSquareFeet = netArea.HasValue ? Rounding.Quantity(netArea.Value) : null;
        AdjustedAreaSquareFeet = adjustedArea.HasValue ? Rounding.Quantity(adjustedArea.Value) : null;
    }
}
