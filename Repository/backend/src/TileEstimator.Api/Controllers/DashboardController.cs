using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Contracts.Dashboard;
using TileEstimator.Domain.Enums;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// SPEC 7 dashboard. Every figure comes from an aggregate query rather than loading rows and
/// counting them in memory, so the page stays fast as the tenant's history grows.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(
    IApplicationDbContext db,
    ICurrentOrganizationService currentOrganization)
    : ControllerBase
{
    private static readonly ProjectStatus[] ActiveStatuses =
    [
        ProjectStatus.Estimating, ProjectStatus.EstimateReady, ProjectStatus.Quoted,
        ProjectStatus.Accepted, ProjectStatus.Scheduled, ProjectStatus.InProgress
    ];

    [HttpGet]
    [HasPermission(Permissions.ProjectRead)]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var currency = await db.OrganizationSettings
            .Where(s => s.OrganizationId == organizationId)
            .Select(s => s.Currency)
            .FirstOrDefaultAsync(ct) ?? "USD";

        var totalProjects = await db.Projects.CountAsync(ct);
        var activeProjects = await db.Projects.CountAsync(p => ActiveStatuses.Contains(p.Status), ct);
        var draftEstimates = await db.Estimates.CountAsync(e => e.Status == EstimateStatus.Draft, ct);

        // One grouped pass over quotes instead of a query per status.
        var quoteStats = await db.Quotes
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(q => q.GrandTotal) })
            .ToListAsync(ct);

        int CountFor(params QuoteStatus[] statuses) =>
            quoteStats.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

        decimal ValueFor(params QuoteStatus[] statuses) =>
            quoteStats.Where(s => statuses.Contains(s.Status)).Sum(s => s.Value);

        // "Sent" for the KPI means it left the building, whatever happened to it since.
        var quotesSent = CountFor(QuoteStatus.Sent, QuoteStatus.Viewed, QuoteStatus.Accepted,
            QuoteStatus.Rejected, QuoteStatus.Expired);
        var quotesAccepted = CountFor(QuoteStatus.Accepted);
        var quotesRejected = CountFor(QuoteStatus.Rejected);

        var totalQuotedValue = ValueFor(QuoteStatus.Sent, QuoteStatus.Viewed, QuoteStatus.Accepted,
            QuoteStatus.Rejected, QuoteStatus.Expired);
        var acceptedValue = ValueFor(QuoteStatus.Accepted);

        var averageEstimateValue = await db.Estimates
            .Where(e => e.Status == EstimateStatus.Finalized)
            .Select(e => (decimal?)e.GrandTotal)
            .AverageAsync(ct) ?? 0m;

        var decided = quotesAccepted + quotesRejected;
        var winRate = decided == 0 ? 0m : decimal.Round(quotesAccepted / (decimal)decided * 100m, 2);

        var kpis = new DashboardKpis(
            totalProjects, activeProjects, draftEstimates, quotesSent, quotesAccepted, quotesRejected,
            decimal.Round(totalQuotedValue, 2), decimal.Round(acceptedValue, 2),
            decimal.Round(averageEstimateValue, 2), winRate, currency);

        var pipeline = Enum.GetValues<QuoteStatus>()
            .Select(status =>
            {
                var match = quoteStats.FirstOrDefault(s => s.Status == status);
                return new PipelineStage(status.ToString(), match?.Count ?? 0,
                    decimal.Round(match?.Value ?? 0m, 2));
            })
            .ToList();

        var recentProjects = await db.Projects.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => new RecentProject(p.Id, p.ProjectNumber, p.Name,
                p.Customer!.CompanyName ?? (p.Customer.FirstName + " " + p.Customer.LastName),
                p.Status.ToString(), p.CreatedAt))
            .ToListAsync(ct);

        var recentEstimates = await db.Estimates.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(8)
            .Select(e => new RecentEstimate(e.Id, e.EstimateNumber + " v" + e.Version,
                e.Project!.Name, e.Status.ToString(), e.GrandTotal, e.CreatedAt))
            .ToListAsync(ct);

        var recentQuotes = await db.Quotes.AsNoTracking()
            .OrderByDescending(q => q.CreatedAt)
            .Take(8)
            .Select(q => new RecentQuote(q.Id, q.QuoteNumber, q.CustomerDisplayName,
                q.Status.ToString(), q.GrandTotal, q.QuoteDate))
            .ToListAsync(ct);

        return Ok(new DashboardResponse(kpis, pipeline, recentProjects, recentEstimates, recentQuotes));
    }
}
