using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services.Quoting;
using TileEstimator.Contracts.Common;
using TileEstimator.Contracts.Quoting;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Quotes;

namespace TileEstimator.Api.Controllers;

/// <summary>SPEC 17 quotes: conversion, PDF, sending and status.</summary>
[ApiController]
[Route("api/quotes")]
[Produces("application/json")]
public sealed class QuotesController(IApplicationDbContext db, QuoteService quotes) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.QuoteRead)]
    [ProducesResponseType(typeof(PagedResult<QuoteSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuoteSummaryResponse>>> List(
        [FromQuery] QuoteQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = db.Quotes.AsNoTracking();

        if (Enum.TryParse<QuoteStatus>(query.Status, true, out var status))
        {
            source = source.Where(q => q.Status == status);
        }

        if (query.ProjectId.HasValue)
        {
            source = source.Where(q => q.ProjectId == query.ProjectId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            source = source.Where(q => q.CustomerId == query.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(q => q.QuoteNumber.Contains(term) ||
                                       q.CustomerDisplayName.Contains(term));
        }

        var total = await source.CountAsync(ct);

        var items = await source
            .OrderByDescending(q => q.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(q => new QuoteSummaryResponse(
                q.Id, q.QuoteNumber, q.Status.ToString(), q.ProjectId, q.Project!.Name,
                q.CustomerDisplayName, q.GrandTotal, q.Currency, q.QuoteDate, q.ExpiresAt,
                q.SentAt, q.RespondedAt))
            .ToListAsync(ct);

        return Ok(new PagedResult<QuoteSummaryResponse>(items, query.Page, query.PageSize, total));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.QuoteRead)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await BuildResponseAsync(await LoadAsync(id, ct), ct));

    /// <summary>Converts a finalized estimate into a quote snapshot.</summary>
    [HttpPost]
    [HasPermission(Permissions.QuoteCreate)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Create(CreateQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quote = await quotes.CreateFromEstimateAsync(request.EstimateId, request.Title,
            request.ScopeOfWork, ct);

        var response = await BuildResponseAsync(await LoadAsync(quote.Id, ct), ct);
        return CreatedAtAction(nameof(Get), new { id = quote.Id }, response);
    }

    [HttpPut("{id:guid}/content")]
    [HasPermission(Permissions.QuoteCreate)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> UpdateContent(Guid id,
        UpdateQuoteContentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quote = await LoadAsync(id, ct);

        // SetContent refuses once the quote has been sent, because a sent quote is a snapshot.
        quote.SetContent(request.Title, request.ScopeOfWork, request.TermsAndConditions,
            request.FooterNote);

        await db.SaveChangesAsync(ct);
        return Ok(await BuildResponseAsync(quote, ct));
    }

    /// <summary>Renders and returns the proposal PDF.</summary>
    [HttpGet("{id:guid}/pdf")]
    [HasPermission(Permissions.QuoteRead)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        var pdf = await quotes.GeneratePdfAsync(id, ct);

        return File(pdf, "application/pdf", $"Quote-{quote.QuoteNumber}.pdf");
    }

    [HttpPost("{id:guid}/send")]
    [HasPermission(Permissions.QuoteSend)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuoteResponse>> Send(Guid id, SendQuoteRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await quotes.SendAsync(id, request.RecipientEmails, request.Message, ct);
        return Ok(await BuildResponseAsync(await LoadAsync(id, ct), ct));
    }

    /// <summary>
    /// Records a decision on the customer's behalf, for when they reply by phone rather than
    /// through the link. Needs quote.approve.
    /// </summary>
    [HttpPost("{id:guid}/record-decision")]
    [HasPermission(Permissions.QuoteApprove)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> RecordDecision(Guid id,
        PublicQuoteDecisionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quote = await LoadAsync(id, ct);

        await quotes.RecordDecisionAsync(quote,
            CustomersController.ParseEnum<QuoteApprovalDecision>(request.Decision, nameof(request.Decision)),
            request.CustomerName, request.CustomerEmail, request.Comments,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.FirstOrDefault(), ct);

        return Ok(await BuildResponseAsync(await LoadAsync(id, ct), ct));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.QuoteCreate)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> Cancel(Guid id, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        quote.Cancel();
        await db.SaveChangesAsync(ct);

        return Ok(await BuildResponseAsync(quote, ct));
    }

    private async Task<Quote> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Quotes
            .Include(q => q.Lines)
            .Include(q => q.Recipients)
            .Include(q => q.Approvals)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
        ?? throw new NotFoundException(nameof(Quote), id);

    private async Task<QuoteResponse> BuildResponseAsync(Quote quote, CancellationToken ct)
    {
        var projectName = await db.Projects
            .Where(p => p.Id == quote.ProjectId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "-";

        var estimateNumber = await db.Estimates
            .Where(e => e.Id == quote.EstimateId)
            .Select(e => e.EstimateNumber + " v" + e.Version)
            .FirstOrDefaultAsync(ct) ?? "-";

        return quote.ToResponse(projectName, estimateNumber);
    }
}
