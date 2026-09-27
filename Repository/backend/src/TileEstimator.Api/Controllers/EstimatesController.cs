using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services;
using TileEstimator.Application.Services.Estimating;
using TileEstimator.Contracts.Common;
using TileEstimator.Contracts.Estimating;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;

namespace TileEstimator.Api.Controllers;

/// <summary>SPEC 15 estimates: lines, pricing, versioning and finalization.</summary>
[ApiController]
[Route("api/estimates")]
[Produces("application/json")]
public sealed class EstimatesController(
    IApplicationDbContext db,
    EstimateService estimates,
    IAuditService audit)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(PagedResult<EstimateSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EstimateSummaryResponse>>> List(
        [FromQuery] EstimateQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = db.Estimates.AsNoTracking();

        if (query.ProjectId.HasValue)
        {
            source = source.Where(e => e.ProjectId == query.ProjectId.Value);
        }

        if (Enum.TryParse<EstimateStatus>(query.Status, true, out var status))
        {
            source = source.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(e => e.EstimateNumber.Contains(term) ||
                                       (e.Title != null && e.Title.Contains(term)));
        }

        var total = await source.CountAsync(ct);

        var items = await source
            .OrderByDescending(e => e.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(e => new EstimateSummaryResponse(
                e.Id, e.EstimateNumber, e.Version, e.EstimateNumber + " v" + e.Version,
                e.Status.ToString(), e.ProjectId, e.Project!.Name,
                e.Project.Customer!.CompanyName ?? (e.Project.Customer.FirstName + " " + e.Project.Customer.LastName),
                e.GrandTotal, e.Currency, e.CreatedAt, e.FinalizedAt))
            .ToListAsync(ct);

        return Ok(new PagedResult<EstimateSummaryResponse>(items, query.Page, query.PageSize, total));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstimateResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await BuildResponseAsync(await LoadAsync(id, ct), ct));

    /// <summary>All versions of an estimate number, newest first (SPEC 15).</summary>
    [HttpGet("{id:guid}/versions")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(IReadOnlyList<EstimateSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EstimateSummaryResponse>>> Versions(Guid id,
        CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);

        var versions = await db.Estimates.AsNoTracking()
            .Where(e => e.EstimateNumber == estimate.EstimateNumber)
            .OrderByDescending(e => e.Version)
            .Select(e => new EstimateSummaryResponse(
                e.Id, e.EstimateNumber, e.Version, e.EstimateNumber + " v" + e.Version,
                e.Status.ToString(), e.ProjectId, e.Project!.Name,
                e.Project.Customer!.CompanyName ?? (e.Project.Customer.FirstName + " " + e.Project.Customer.LastName),
                e.GrandTotal, e.Currency, e.CreatedAt, e.FinalizedAt))
            .ToListAsync(ct);

        return Ok(versions);
    }

    [HttpPost]
    [HasPermission(Permissions.EstimateCreate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<EstimateResponse>> Create(CreateEstimateRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await estimates.CreateFromProjectAsync(request.ProjectId, request.Title, ct);
        var response = await BuildResponseAsync(await LoadAsync(estimate.Id, ct), ct);

        return CreatedAtAction(nameof(Get), new { id = estimate.Id }, response);
    }

    /// <summary>Re-runs Cost then Pricing over the current lines and returns the new totals.</summary>
    [HttpPost("{id:guid}/calculate")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstimateResponse>> Calculate(Guid id, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    /// <summary>
    /// Rebuilds the lines from the project's rooms and surfaces. This discards manual lines and
    /// price overrides, so the UI confirms before calling it.
    /// </summary>
    [HttpPost("{id:guid}/rebuild")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> Rebuild(Guid id, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        await estimates.RebuildLinesFromProjectAsync(estimate, ct);
        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    [HttpPut("{id:guid}/pricing")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstimateResponse>> UpdatePricing(Guid id,
        UpdateEstimatePricingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await LoadAsync(id, ct);
        ApplyConcurrencyToken(estimate, request.RowVersion);

        estimate.SetPricingStrategy(
            CustomersController.ParseEnum<PricingStrategy>(request.PricingStrategy, nameof(request.PricingStrategy)),
            request.MarkupPercentage, request.MarginPercentage, request.FixedMarkupAmount);

        estimate.SetOverhead(request.OverheadPercentage);

        estimate.SetDiscount(
            CustomersController.ParseEnum<DiscountType>(request.DiscountType, nameof(request.DiscountType)),
            request.DiscountValue);

        estimate.SetTax(request.TaxRatePercentage,
            CustomersController.ParseEnum<TaxBasis>(request.TaxBasis, nameof(request.TaxBasis)),
            request.DiscountBeforeTax);

        await estimates.RecalculateAsync(estimate, ct);

        await audit.RecordAsync(estimate.OrganizationId, AuditAction.PriceChange, nameof(Estimate),
            estimate.Id.ToString(),
            $"Pricing updated on {estimate.DisplayNumber}: {estimate.PricingStrategy}, total {estimate.GrandTotal:F2}.",
            ct);

        return Ok(await BuildResponseAsync(estimate, ct));
    }

    [HttpPut("{id:guid}/details")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> UpdateDetails(Guid id,
        UpdateEstimateDetailsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await LoadAsync(id, ct);
        estimate.SetDetails(request.Title, request.Notes);
        await db.SaveChangesAsync(ct);

        return Ok(await BuildResponseAsync(estimate, ct));
    }

    // --- Lines -------------------------------------------------------------------------------

    [HttpPost("{id:guid}/lines")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> AddLine(Guid id,
        SaveEstimateLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await LoadAsync(id, ct);

        await estimates.AddManualLineAsync(estimate,
            CustomersController.ParseEnum<EstimateLineCategory>(request.Category, nameof(request.Category)),
            request.Description, request.Quantity, request.Unit, request.UnitCost, request.UnitPrice,
            request.RoomId, ct);

        return Ok(await BuildResponseAsync(estimate, ct));
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> UpdateLine(Guid id, Guid lineId,
        SaveEstimateLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await LoadAsync(id, ct);
        EnsureEditable(estimate);

        var line = await FindLineAsync(id, lineId, ct);

        line.Update(request.Description, request.Quantity, request.Quantity, request.Unit,
            request.UnitCost, request.UnitPrice, line.WastePercentage, request.Notes);

        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    /// <summary>Copies a line, so a repeated item does not have to be typed twice.</summary>
    [HttpPost("{id:guid}/lines/{lineId:guid}/duplicate")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> DuplicateLine(Guid id, Guid lineId,
        CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        EnsureEditable(estimate);

        var source = await FindLineAsync(id, lineId, ct);

        var copy = source.CopyTo(estimate.Id);
        copy.SetSortOrder(estimate.Lines.Count == 0 ? 0 : estimate.Lines.Max(l => l.SortOrder) + 1);

        db.EstimateLines.Add(copy);
        await db.SaveChangesAsync(ct);

        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    /// <summary>
    /// Replaces a calculated price with one the estimator typed. The original is kept, so the
    /// override stays visible on the line and in reports.
    /// </summary>
    [HttpPost("{id:guid}/lines/{lineId:guid}/override-price")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> OverridePrice(Guid id, Guid lineId,
        OverridePriceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estimate = await LoadAsync(id, ct);
        EnsureEditable(estimate);

        var line = await FindLineAsync(id, lineId, ct);
        var original = line.UnitPrice;
        line.OverrideUnitPrice(request.UnitPrice, request.Reason);

        await estimates.RecalculateAsync(estimate, ct);

        await audit.RecordAsync(estimate.OrganizationId, AuditAction.PriceChange, nameof(EstimateLine),
            line.Id.ToString(),
            $"Price on '{line.Description}' overridden from {original:F2} to {line.UnitPrice:F2}.", ct);

        return Ok(await BuildResponseAsync(estimate, ct));
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}/override-price")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> ClearOverride(Guid id, Guid lineId,
        CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        EnsureEditable(estimate);

        var line = await FindLineAsync(id, lineId, ct);
        line.ClearPriceOverride();

        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [HasPermission(Permissions.EstimateUpdate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EstimateResponse>> DeleteLine(Guid id, Guid lineId, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        EnsureEditable(estimate);

        var line = await FindLineAsync(id, lineId, ct);

        db.EstimateLines.Remove(line);
        await db.SaveChangesAsync(ct);

        await estimates.RecalculateAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    // --- Lifecycle ----------------------------------------------------------------------------

    [HttpPost("{id:guid}/finalize")]
    [HasPermission(Permissions.EstimateApprove)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstimateResponse>> Finalize(Guid id, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        await estimates.FinalizeAsync(estimate, ct);
        return Ok(await BuildResponseAsync(estimate, ct));
    }

    /// <summary>Starts a new version of a finalized estimate (SPEC 15).</summary>
    [HttpPost("{id:guid}/new-version")]
    [HasPermission(Permissions.EstimateCreate)]
    [ProducesResponseType(typeof(EstimateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstimateResponse>> NewVersion(Guid id, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);
        var next = await estimates.CreateNextVersionAsync(estimate, ct);
        var response = await BuildResponseAsync(await LoadAsync(next.Id, ct), ct);

        return CreatedAtAction(nameof(Get), new { id = next.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.EstimateDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var estimate = await LoadAsync(id, ct);

        // Financial history is never removed; a finalized estimate can only be superseded.
        if (estimate.Status == EstimateStatus.Finalized)
        {
            throw new ConflictException(
                "A finalized estimate cannot be deleted. Create a new version instead.");
        }

        db.Estimates.Remove(estimate);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(estimate.OrganizationId, AuditAction.Delete, nameof(Estimate),
            id.ToString(), $"Draft estimate {estimate.DisplayNumber} deleted.", ct);

        return NoContent();
    }

    // --- Helpers -------------------------------------------------------------------------------

    private async Task<Estimate> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Estimates.Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException(nameof(Estimate), id);

    private async Task<EstimateLine> FindLineAsync(Guid estimateId, Guid lineId, CancellationToken ct) =>
        await db.EstimateLines.FirstOrDefaultAsync(l => l.Id == lineId && l.EstimateId == estimateId, ct)
        ?? throw new NotFoundException(nameof(EstimateLine), lineId);

    private async Task<EstimateResponse> BuildResponseAsync(Estimate estimate, CancellationToken ct)
    {
        var projectName = await db.Projects
            .Where(p => p.Id == estimate.ProjectId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "-";

        var roomNames = await db.Rooms
            .Where(r => r.ProjectId == estimate.ProjectId)
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        return estimate.ToResponse(projectName, roomNames);
    }

    private static void EnsureEditable(Estimate estimate)
    {
        if (!estimate.IsEditable)
        {
            throw new ConflictException(
                $"Estimate {estimate.DisplayNumber} is finalized. Create a new version to make changes.");
        }
    }

    /// <summary>
    /// Applies the client's concurrency token so a stale edit is rejected with a 409 rather
    /// than silently overwriting another estimator's work (SPEC 18).
    /// </summary>
    private void ApplyConcurrencyToken(Estimate estimate, string? encodedRowVersion)
    {
        var rowVersion = Mappers.DecodeRowVersion(encodedRowVersion);
        if (rowVersion is null)
        {
            return;
        }

        db.Estimates.Entry(estimate).Property(e => e.RowVersion).OriginalValue = rowVersion;
    }
}
