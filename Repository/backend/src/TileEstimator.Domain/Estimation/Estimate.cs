using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 15 estimate. Carries the full pricing configuration that was in force when it was
/// calculated, so re-opening an old estimate shows the numbers the estimator actually quoted.
/// Once finalized it is immutable: editing it produces a new version (EST-1001 v2).
/// </summary>
public class Estimate : TenantEntity, IHasRowVersion
{
    public Guid ProjectId { get; private set; }
    public string EstimateNumber { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;

    /// <summary>The estimate this version replaced, forming the version chain for EST-1001.</summary>
    public Guid? SupersedesEstimateId { get; private set; }

    public EstimateStatus Status { get; private set; } = EstimateStatus.Draft;
    public string? Title { get; private set; }
    public string? Notes { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    // --- Pricing configuration snapshot (SPEC 14) ---
    public PricingStrategy PricingStrategy { get; private set; } = PricingStrategy.Markup;
    public decimal MarkupPercentage { get; private set; }
    public decimal MarginPercentage { get; private set; }
    public decimal FixedMarkupAmount { get; private set; }
    public decimal OverheadPercentage { get; private set; }
    public DiscountType DiscountType { get; private set; } = DiscountType.None;
    public decimal DiscountValue { get; private set; }
    public decimal TaxRatePercentage { get; private set; }
    public TaxBasis TaxBasis { get; private set; } = TaxBasis.MaterialsAndLabor;
    public bool DiscountBeforeTax { get; private set; } = true;

    // --- Calculated totals (SPEC 15) ---
    public decimal MaterialCost { get; private set; }
    public decimal LaborCost { get; private set; }
    public decimal OtherCost { get; private set; }
    public decimal OverheadAmount { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal MarkupAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    public DateTime? FinalizedAt { get; private set; }
    public Guid? FinalizedBy { get; private set; }

    public byte[]? RowVersion { get; set; }

    public Project? Project { get; private set; }
    public ICollection<EstimateLine> Lines { get; private set; } = new List<EstimateLine>();

    private Estimate() { }

    public static Estimate Create(Guid organizationId, Guid projectId, string estimateNumber, string? title,
        string currency, PricingStrategy strategy, decimal markupPercentage, decimal marginPercentage,
        decimal overheadPercentage, decimal taxRatePercentage, TaxBasis taxBasis, bool discountBeforeTax)
    {
        DomainException.Require(projectId != Guid.Empty, "An estimate must belong to a project.");
        DomainException.Require(!string.IsNullOrWhiteSpace(estimateNumber), "Estimate number is required.");

        var estimate = new Estimate
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            EstimateNumber = estimateNumber,
            Title = title,
            Currency = currency,
            OverheadPercentage = overheadPercentage,
            TaxRatePercentage = taxRatePercentage,
            TaxBasis = taxBasis,
            DiscountBeforeTax = discountBeforeTax
        };
        estimate.SetPricingStrategy(strategy, markupPercentage, marginPercentage, 0m);
        return estimate;
    }

    /// <summary>
    /// Starts the next version of a finalized estimate. The previous version is marked Superseded
    /// and keeps its numbers untouched, so financial history is never rewritten.
    /// </summary>
    public Estimate CreateNextVersion()
    {
        DomainException.Require(Status == EstimateStatus.Finalized,
            "Only a finalized estimate can be versioned. Edit the draft instead.");

        var next = new Estimate
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            EstimateNumber = EstimateNumber,
            Version = Version + 1,
            SupersedesEstimateId = Id,
            Title = Title,
            Notes = Notes,
            Currency = Currency,
            PricingStrategy = PricingStrategy,
            MarkupPercentage = MarkupPercentage,
            MarginPercentage = MarginPercentage,
            FixedMarkupAmount = FixedMarkupAmount,
            OverheadPercentage = OverheadPercentage,
            DiscountType = DiscountType,
            DiscountValue = DiscountValue,
            TaxRatePercentage = TaxRatePercentage,
            TaxBasis = TaxBasis,
            DiscountBeforeTax = DiscountBeforeTax
        };

        foreach (var line in Lines.OrderBy(l => l.SortOrder))
        {
            next.Lines.Add(line.CopyTo(next.Id));
        }

        Status = EstimateStatus.Superseded;
        return next;
    }

    public bool IsEditable => Status is EstimateStatus.Draft or EstimateStatus.InReview;

    private void EnsureEditable() =>
        DomainException.Require(IsEditable, "This estimate is finalized and can no longer be changed. Create a new version.");

    public void SetPricingStrategy(PricingStrategy strategy, decimal markupPercentage,
        decimal marginPercentage, decimal fixedMarkupAmount)
    {
        EnsureEditable();
        DomainException.Require(markupPercentage >= 0m, "Markup cannot be negative.");
        DomainException.Require(marginPercentage >= 0m && marginPercentage < 100m,
            "Margin must be at least 0% and below 100%.");
        DomainException.Require(fixedMarkupAmount >= 0m, "Fixed markup cannot be negative.");

        PricingStrategy = strategy;
        MarkupPercentage = Rounding.Quantity(markupPercentage);
        MarginPercentage = Rounding.Quantity(marginPercentage);
        FixedMarkupAmount = Rounding.Money(fixedMarkupAmount);
    }

    public void SetOverhead(decimal overheadPercentage)
    {
        EnsureEditable();
        DomainException.Require(overheadPercentage >= 0m, "Overhead cannot be negative.");
        OverheadPercentage = Rounding.Quantity(overheadPercentage);
    }

    public void SetDiscount(DiscountType type, decimal value)
    {
        EnsureEditable();
        DomainException.Require(value >= 0m, "Discount cannot be negative.");
        DomainException.Require(type != DiscountType.Percentage || value <= 100m,
            "A percentage discount cannot exceed 100%.");

        DiscountType = type;
        DiscountValue = type == DiscountType.None ? 0m : Rounding.Quantity(value);
    }

    public void SetTax(decimal taxRatePercentage, TaxBasis basis, bool discountBeforeTax)
    {
        EnsureEditable();
        DomainException.Require(taxRatePercentage >= 0m, "Tax rate cannot be negative.");
        TaxRatePercentage = Rounding.Quantity(taxRatePercentage);
        TaxBasis = basis;
        DiscountBeforeTax = discountBeforeTax;
    }

    public void SetDetails(string? title, string? notes)
    {
        EnsureEditable();
        Title = title;
        Notes = notes;
    }

    public void AddLine(EstimateLine line)
    {
        EnsureEditable();
        Lines.Add(line);
    }

    public void RemoveLine(EstimateLine line)
    {
        EnsureEditable();
        Lines.Remove(line);
    }

    /// <summary>Writes the totals produced by the cost and pricing engines back onto the estimate.</summary>
    public void ApplyTotals(decimal materialCost, decimal laborCost, decimal otherCost, decimal overheadAmount,
        decimal totalCost, decimal markupAmount, decimal discountAmount, decimal taxAmount,
        decimal subtotal, decimal grandTotal)
    {
        EnsureEditable();
        MaterialCost = Rounding.Money(materialCost);
        LaborCost = Rounding.Money(laborCost);
        OtherCost = Rounding.Money(otherCost);
        OverheadAmount = Rounding.Money(overheadAmount);
        TotalCost = Rounding.Money(totalCost);
        MarkupAmount = Rounding.Money(markupAmount);
        DiscountAmount = Rounding.Money(discountAmount);
        TaxAmount = Rounding.Money(taxAmount);
        Subtotal = Rounding.Money(subtotal);
        GrandTotal = Rounding.Money(grandTotal);
    }

    public void SubmitForReview()
    {
        DomainException.Require(Status == EstimateStatus.Draft, "Only a draft estimate can be sent for review.");
        Status = EstimateStatus.InReview;
    }

    public void Finalize(Guid userId, DateTime utcNow)
    {
        DomainException.Require(IsEditable, "This estimate is already finalized.");
        DomainException.Require(Lines.Count > 0, "An estimate needs at least one line before it can be finalized.");
        Status = EstimateStatus.Finalized;
        FinalizedAt = utcNow;
        FinalizedBy = userId;
    }

    public void Cancel()
    {
        DomainException.Require(Status != EstimateStatus.Finalized,
            "A finalized estimate cannot be cancelled. Create a new version instead.");
        Status = EstimateStatus.Cancelled;
    }

    public string DisplayNumber => EstimateNumber + " v" + Version;
}
