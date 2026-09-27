using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services.Quoting;
using TileEstimator.Contracts.Quoting;
using TileEstimator.Domain.Enums;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// SPEC 17 public quote page. Unauthenticated: the customer has no account and holds only an
/// unguessable, expiring token.
/// <para>
/// Everything here is deliberately narrow. The token is the sole credential and is matched
/// against a stored hash; an unknown, expired or revoked token gets the same flat 404, so the
/// endpoint cannot be probed to learn which tokens exist. The response carries no cost, no
/// margin and no internal identifiers, and nothing on this controller can reach any other
/// record. Rate limited per IP.
/// </para>
/// </summary>
[ApiController]
[Route("api/public/quotes")]
[AllowAnonymous]
[EnableRateLimiting("public-quote")]
[Produces("application/json")]
public sealed class PublicQuotesController(
    IApplicationDbContext db,
    QuoteService quotes,
    ICurrentOrganizationService currentOrganization,
    IDateTimeProvider clock)
    : ControllerBase
{
    /// <summary>Opens a quote by its public token. The first open records the view.</summary>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(PublicQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicQuoteResponse>> Get(string token, CancellationToken ct)
    {
        var quote = await quotes.GetByPublicTokenAsync(token, ct);

        if (quote is null)
        {
            // Same answer for an unknown, expired or revoked token.
            return NotFound(new ProblemDetails
            {
                Title = "Quote not available",
                Detail = "This link is no longer valid. Please ask for a new one.",
                Status = StatusCodes.Status404NotFound
            });
        }

        await quotes.MarkViewedAsync(quote, ct);

        var projectName = await GetProjectNameAsync(quote.ProjectId, ct);
        return Ok(quote.ToPublicResponse(projectName, clock.UtcNow));
    }

    /// <summary>Downloads the proposal PDF for a valid public link.</summary>
    [HttpGet("{token}/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(string token, CancellationToken ct)
    {
        var quote = await quotes.GetByPublicTokenAsync(token, ct);

        if (quote is null)
        {
            return NotFound();
        }

        // Regenerating runs inside a tenant bypass, because the customer has no organization.
        using var bypass = currentOrganization.BypassTenantFilter();
        var pdf = await quotes.GeneratePdfAsync(quote.Id, ct);

        return File(pdf, "application/pdf", $"Quote-{quote.QuoteNumber}.pdf");
    }

    /// <summary>
    /// Records the customer's decision: accept, reject or request changes. This is an approval
    /// record with a timestamp and IP address, not a legally binding electronic signature.
    /// </summary>
    [HttpPost("{token}/respond")]
    [ProducesResponseType(typeof(PublicQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicQuoteResponse>> Respond(string token,
        PublicQuoteDecisionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quote = await quotes.GetByPublicTokenAsync(token, ct);

        if (quote is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Quote not available",
                Detail = "This link is no longer valid. Please ask for a new one.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var decision = Enum.TryParse<QuoteApprovalDecision>(request.Decision, true, out var parsed)
            ? parsed
            : throw new ValidationFailedException(nameof(request.Decision),
                "Choose Accepted, Rejected or ChangesRequested.");

        await quotes.RecordDecisionAsync(quote, decision, request.CustomerName, request.CustomerEmail,
            request.Comments,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.FirstOrDefault(),
            ct);

        var projectName = await GetProjectNameAsync(quote.ProjectId, ct);
        return Ok(quote.ToPublicResponse(projectName, clock.UtcNow));
    }

    private async Task<string?> GetProjectNameAsync(Guid projectId, CancellationToken ct)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        return await db.Projects
            .IgnoreQueryFilters()
            .Where(p => p.Id == projectId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
    }
}
