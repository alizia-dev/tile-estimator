using Microsoft.EntityFrameworkCore;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Application.Engines.Costing;
using TileEstimator.Application.Engines.Pricing;
using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Application.Services.Estimating;

/// <summary>
/// SPEC 15. Owns the estimate lifecycle: build from a project's surfaces, recalculate through
/// Takeoff to Cost to Pricing, finalize, and version.
/// <para>
/// Every line stores the price, waste percentage and labor rate used at the moment it was
/// calculated. Nothing here reads the catalog when totalling an existing estimate, which is
/// what makes a finalized estimate stable no matter what happens to prices afterwards.
/// </para>
/// </summary>
public sealed class EstimateService(
    IApplicationDbContext db,
    TakeoffBuilder takeoffBuilder,
    ITakeoffEngine takeoffEngine,
    ICostCalculationEngine costEngine,
    IPricingEngine pricingEngine,
    ICurrentOrganizationService currentOrganization,
    ICurrentUserService currentUser,
    INumberSequenceService numbers,
    IDateTimeProvider clock,
    IAuditService audit)
{
    /// <summary>
    /// Creates a draft estimate for a project, expanding every surface through the engines.
    /// Pricing defaults come from organization settings and are copied onto the estimate, so
    /// changing the defaults later does not move an estimate already in progress.
    /// </summary>
    public async Task<Estimate> CreateFromProjectAsync(Guid projectId, string? title,
        CancellationToken cancellationToken)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), projectId);

        var settings = await db.OrganizationSettings
                           .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken)
                       ?? throw new NotFoundException("OrganizationSettings", organizationId);

        var number = await numbers.NextAsync(organizationId, "Estimate",
            settings.EstimateNumberPrefix, cancellationToken);

        var estimate = Estimate.Create(
            organizationId, projectId, number, title ?? project.Name, settings.Currency,
            settings.DefaultPricingStrategy, settings.DefaultMarkupPercentage,
            settings.DefaultMarginPercentage, settings.DefaultOverheadPercentage,
            settings.DefaultTaxRatePercentage, settings.TaxBasis, settings.DiscountBeforeTax);

        db.Estimates.Add(estimate);
        await db.SaveChangesAsync(cancellationToken);

        await RebuildLinesFromProjectAsync(estimate, cancellationToken);
        await RecalculateAsync(estimate, cancellationToken);

        project.ChangeStatus(ProjectStatus.Estimating);
        await db.SaveChangesAsync(cancellationToken);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Estimate),
            estimate.Id.ToString(), $"Estimate {estimate.DisplayNumber} created.", cancellationToken);

        return estimate;
    }

    /// <summary>
    /// Replaces the estimate's lines with a fresh expansion of the project's surfaces.
    /// Manually added lines and price overrides are discarded, which is why the API asks
    /// before doing this to an estimate that has been edited.
    /// </summary>
    public async Task RebuildLinesFromProjectAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        EnsureEditable(estimate);

        var surfaces = await takeoffBuilder.BuildForProjectAsync(estimate.ProjectId, cancellationToken);
        var takeoff = takeoffEngine.Calculate(surfaces);

        var existing = await db.EstimateLines
            .Where(l => l.EstimateId == estimate.Id)
            .ToListAsync(cancellationToken);

        db.EstimateLines.RemoveRange(existing);

        var sortOrder = 0;
        foreach (var line in takeoff.Lines)
        {
            var estimateLine = EstimateLine.Create(
                estimate.OrganizationId, estimate.Id, line.Category, line.Description,
                line.Quantity, line.PurchaseQuantity, line.Unit,
                line.UnitCost, line.UnitPrice, line.WastePercentage,
                line.Breakdown.Formula, sortOrder++);

            estimateLine.LinkSources(line.RoomId, line.SurfaceId, line.TileId, line.MaterialId,
                line.LaborRateId, line.AssemblyId);

            // New lines are tracked through the DbSet only. Putting a new entity into the
            // parent's navigation instead leaves its state up to change detection, which has
            // bitten us both ways: counted twice, or mistaken for an existing row and UPDATEd.
            db.EstimateLines.Add(estimateLine);
        }

        await db.SaveChangesAsync(cancellationToken);
        await ReloadLinesAsync(estimate, cancellationToken);
    }

    /// <summary>
    /// Refreshes the in-memory line collection from the database, so callers and the response
    /// mapper see exactly the rows that were persisted.
    /// </summary>
    private async Task ReloadLinesAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        var lines = await db.EstimateLines
            .Where(l => l.EstimateId == estimate.Id)
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        estimate.Lines.Clear();
        foreach (var line in lines)
        {
            estimate.Lines.Add(line);
        }
    }

    /// <summary>
    /// Re-runs Cost and Pricing over the estimate's existing lines and writes the totals back.
    /// The line snapshots are the input, so this never reaches into the catalog.
    /// </summary>
    public async Task<PricingResult> RecalculateAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        EnsureEditable(estimate);

        // Read the lines back from the database rather than trusting the navigation collection,
        // so the totals always reflect exactly what is stored.
        var lines = await db.EstimateLines
            .Where(l => l.EstimateId == estimate.Id)
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        var costInput = new CostCalculationInput
        {
            OverheadPercentage = estimate.OverheadPercentage,
            Lines = lines
                .Select(l => new CostLineInput(
                    l.Category,
                    l.Description,
                    l.PurchaseQuantity > 0m ? l.PurchaseQuantity : l.Quantity,
                    l.Unit,
                    l.UnitCost,
                    l.UnitPrice))
                .ToList()
        };

        var cost = costEngine.Calculate(costInput);

        var pricing = pricingEngine.Calculate(new PricingInput
        {
            TotalCost = cost.TotalCost,
            MaterialCost = cost.MaterialCost,
            LaborCost = cost.LaborCost,
            Strategy = estimate.PricingStrategy,
            MarkupPercentage = estimate.MarkupPercentage,
            MarginPercentage = estimate.MarginPercentage,
            FixedMarkupAmount = estimate.FixedMarkupAmount,
            DiscountType = estimate.DiscountType,
            DiscountValue = estimate.DiscountValue,
            TaxRatePercentage = estimate.TaxRatePercentage,
            TaxBasis = estimate.TaxBasis,
            DiscountBeforeTax = estimate.DiscountBeforeTax
        });

        estimate.ApplyTotals(
            cost.MaterialCost,
            cost.LaborCost,
            cost.EquipmentCost + cost.DeliveryCost + cost.OtherCost,
            cost.OverheadAmount,
            cost.TotalCost,
            pricing.MarkupAmount,
            pricing.DiscountAmount,
            pricing.TaxAmount,
            pricing.Subtotal,
            pricing.GrandTotal);

        await db.SaveChangesAsync(cancellationToken);
        return pricing;
    }

    /// <summary>Finalizes an estimate, making it immutable and eligible for conversion to a quote.</summary>
    public async Task FinalizeAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        var userId = currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");

        // Totals are refreshed first so a finalized estimate can never carry stale numbers.
        await RecalculateAsync(estimate, cancellationToken);

        estimate.Finalize(userId, clock.UtcNow);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == estimate.ProjectId, cancellationToken);
        if (project is not null && project.Status is ProjectStatus.Draft or ProjectStatus.Estimating)
        {
            project.ChangeStatus(ProjectStatus.EstimateReady);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.RecordAsync(estimate.OrganizationId, AuditAction.Finalize, nameof(Estimate),
            estimate.Id.ToString(),
            $"Estimate {estimate.DisplayNumber} finalized at {estimate.GrandTotal:F2} {estimate.Currency}.",
            cancellationToken);
    }

    /// <summary>
    /// SPEC 15 versioning: a finalized estimate is never edited in place. This copies it to a
    /// new version with its lines intact and marks the previous one Superseded.
    /// </summary>
    public async Task<Estimate> CreateNextVersionAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        await ReloadLinesAsync(estimate, cancellationToken);

        var next = estimate.CreateNextVersion();

        // `next` is itself new, so adding it cascades an INSERT to the copied lines it carries.
        db.Estimates.Add(next);

        await db.SaveChangesAsync(cancellationToken);

        await audit.RecordAsync(estimate.OrganizationId, AuditAction.Create, nameof(Estimate),
            next.Id.ToString(),
            $"Estimate {next.DisplayNumber} created from {estimate.DisplayNumber}.", cancellationToken);

        return next;
    }

    /// <summary>Adds a line the estimator typed in by hand, outside the assembly expansion.</summary>
    public async Task<EstimateLine> AddManualLineAsync(Estimate estimate, EstimateLineCategory category,
        string description, decimal quantity, string unit, decimal unitCost, decimal unitPrice,
        Guid? roomId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        EnsureEditable(estimate);

        await ReloadLinesAsync(estimate, cancellationToken);

        var sortOrder = estimate.Lines.Count == 0 ? 0 : estimate.Lines.Max(l => l.SortOrder) + 1;

        var line = EstimateLine.Create(estimate.OrganizationId, estimate.Id, category, description,
            quantity, quantity, unit, unitCost, unitPrice, 0m,
            $"Manual entry: {quantity:0.####} {unit} at {unitPrice:C2}", sortOrder);

        line.LinkSources(roomId, null, null, null, null, null);

        db.EstimateLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);

        await RecalculateAsync(estimate, cancellationToken);
        await ReloadLinesAsync(estimate, cancellationToken);
        return line;
    }

    public async Task<Estimate> GetForEditAsync(Guid estimateId, CancellationToken cancellationToken)
    {
        var estimate = await db.Estimates
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == estimateId, cancellationToken)
            ?? throw new NotFoundException(nameof(Estimate), estimateId);

        return estimate;
    }

    private static void EnsureEditable(Estimate estimate)
    {
        if (!estimate.IsEditable)
        {
            throw new ConflictException(
                $"Estimate {estimate.DisplayNumber} is finalized. Create a new version to make changes.");
        }
    }
}
