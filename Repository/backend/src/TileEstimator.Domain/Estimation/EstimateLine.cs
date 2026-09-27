using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 15 estimate line. Every price, waste percentage and labor rate on this row is a
/// snapshot taken when the line was calculated. Changing the catalog later never moves these
/// numbers, which is what makes an old estimate still mean what it said.
/// </summary>
public class EstimateLine : TenantEntity
{
    public Guid EstimateId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? SurfaceId { get; private set; }

    public EstimateLineCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;

    /// <summary>Calculated quantity before purchase rounding, e.g. 20.16 boxes.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>What actually gets bought, e.g. 21 boxes. Equal to Quantity for non-packaged items.</summary>
    public decimal PurchaseQuantity { get; private set; }

    public string Unit { get; private set; } = UnitOfMeasure.Each;

    // --- Snapshots: these never track the catalog ---
    public decimal UnitCost { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal WastePercentage { get; private set; }

    public decimal TotalCost { get; private set; }
    public decimal TotalPrice { get; private set; }

    /// <summary>Source catalog row, kept for reporting only. The money above does not read from it.</summary>
    public Guid? TileId { get; private set; }
    public Guid? MaterialId { get; private set; }
    public Guid? LaborRateId { get; private set; }
    public Guid? AssemblyId { get; private set; }

    /// <summary>
    /// Human-readable derivation of this line, e.g. "450 SF x $8.00/SF = $3,600.00".
    /// Written by the engines so the grid can explain any number without recomputing it.
    /// </summary>
    public string? CalculationReference { get; private set; }

    /// <summary>True when an estimator typed over the calculated price.</summary>
    public bool IsPriceOverridden { get; private set; }

    public decimal? OriginalUnitPrice { get; private set; }
    public string? OverrideReason { get; private set; }
    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }

    public Estimate? Estimate { get; private set; }

    private EstimateLine() { }

    public static EstimateLine Create(Guid organizationId, Guid estimateId, EstimateLineCategory category,
        string description, decimal quantity, decimal purchaseQuantity, string unit, decimal unitCost,
        decimal unitPrice, decimal wastePercentage, string? calculationReference, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(description), "Line description is required.");
        DomainException.Require(quantity >= 0m, "Quantity cannot be negative.");
        DomainException.Require(purchaseQuantity >= 0m, "Purchase quantity cannot be negative.");
        DomainException.Require(unitCost >= 0m, "Unit cost cannot be negative.");
        DomainException.Require(unitPrice >= 0m, "Unit price cannot be negative.");
        DomainException.Require(wastePercentage >= 0m, "Waste cannot be negative.");

        var line = new EstimateLine
        {
            OrganizationId = organizationId,
            EstimateId = estimateId,
            Category = category,
            Description = description.Trim(),
            Quantity = Rounding.Quantity(quantity),
            PurchaseQuantity = Rounding.Quantity(purchaseQuantity),
            Unit = unit.Trim().ToUpperInvariant(),
            UnitCost = Rounding.Money(unitCost),
            UnitPrice = Rounding.Money(unitPrice),
            WastePercentage = Rounding.Quantity(wastePercentage),
            CalculationReference = calculationReference,
            SortOrder = sortOrder
        };
        line.RecalculateTotals();
        return line;
    }

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

    public void Update(string description, decimal quantity, decimal purchaseQuantity, string unit,
        decimal unitCost, decimal unitPrice, decimal wastePercentage, string? notes)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(description), "Line description is required.");
        DomainException.Require(quantity >= 0m, "Quantity cannot be negative.");
        DomainException.Require(unitCost >= 0m, "Unit cost cannot be negative.");
        DomainException.Require(unitPrice >= 0m, "Unit price cannot be negative.");

        Description = description.Trim();
        Quantity = Rounding.Quantity(quantity);
        PurchaseQuantity = Rounding.Quantity(purchaseQuantity);
        Unit = unit.Trim().ToUpperInvariant();
        UnitCost = Rounding.Money(unitCost);
        UnitPrice = Rounding.Money(unitPrice);
        WastePercentage = Rounding.Quantity(wastePercentage);
        Notes = notes;
        RecalculateTotals();
    }

    /// <summary>
    /// Replaces the calculated price with one the estimator typed. The original is kept so the
    /// override is visible on the line and in reports rather than silently folded into the total.
    /// </summary>
    public void OverrideUnitPrice(decimal newUnitPrice, string? reason)
    {
        DomainException.Require(newUnitPrice >= 0m, "An overridden price cannot be negative.");
        OriginalUnitPrice ??= UnitPrice;
        UnitPrice = Rounding.Money(newUnitPrice);
        IsPriceOverridden = true;
        OverrideReason = reason;
        RecalculateTotals();
    }

    public void ClearPriceOverride()
    {
        if (!IsPriceOverridden) return;
        UnitPrice = OriginalUnitPrice ?? UnitPrice;
        OriginalUnitPrice = null;
        IsPriceOverridden = false;
        OverrideReason = null;
        RecalculateTotals();
    }

    public void SetCalculationReference(string? reference) => CalculationReference = reference;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Deep-copies this line onto a new estimate version, override and snapshots intact.</summary>
    public EstimateLine CopyTo(Guid estimateId) => new()
    {
        OrganizationId = OrganizationId,
        EstimateId = estimateId,
        RoomId = RoomId,
        SurfaceId = SurfaceId,
        Category = Category,
        Description = Description,
        Quantity = Quantity,
        PurchaseQuantity = PurchaseQuantity,
        Unit = Unit,
        UnitCost = UnitCost,
        UnitPrice = UnitPrice,
        WastePercentage = WastePercentage,
        TotalCost = TotalCost,
        TotalPrice = TotalPrice,
        TileId = TileId,
        MaterialId = MaterialId,
        LaborRateId = LaborRateId,
        AssemblyId = AssemblyId,
        CalculationReference = CalculationReference,
        IsPriceOverridden = IsPriceOverridden,
        OriginalUnitPrice = OriginalUnitPrice,
        OverrideReason = OverrideReason,
        Notes = Notes,
        SortOrder = SortOrder
    };

    /// <summary>
    /// Cost and price extend the purchase quantity, because the contractor pays for the whole
    /// box even when the job only consumes part of it.
    /// </summary>
    private void RecalculateTotals()
    {
        var billable = PurchaseQuantity > 0m ? PurchaseQuantity : Quantity;
        TotalCost = Rounding.Money(UnitCost * billable);
        TotalPrice = Rounding.Money(UnitPrice * billable);
    }
}
