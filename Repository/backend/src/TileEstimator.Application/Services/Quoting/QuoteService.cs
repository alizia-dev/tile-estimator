using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Quotes;

namespace TileEstimator.Application.Services.Quoting;

public sealed record QuoteOptions(string WebAppBaseUrl, int PublicLinkGraceDays);

/// <summary>
/// SPEC 17. Converts a finalized estimate into a quote snapshot, renders the PDF, sends it and
/// records the customer's response.
/// <para>
/// A quote copies everything it needs at conversion time: line prices, the customer's details
/// and the company's own details. Nothing about it changes afterwards when the catalog or the
/// organization profile does.
/// </para>
/// </summary>
public sealed class QuoteService(
    IApplicationDbContext db,
    ICurrentOrganizationService currentOrganization,
    INumberSequenceService numbers,
    IQuotePdfGenerator pdfGenerator,
    IQuoteDeliveryProvider deliveryProvider,
    IFileStorageProvider fileStorage,
    IDateTimeProvider clock,
    IAuditService audit,
    QuoteOptions options,
    ILogger<QuoteService> logger)
{
    /// <summary>
    /// SPEC 17 quote engine: converts a finalized estimate into a customer-facing snapshot.
    /// Only a finalized estimate may be converted, so a quote can never be built on numbers
    /// that are still moving.
    /// </summary>
    public async Task<Quote> CreateFromEstimateAsync(Guid estimateId, string? title, string? scopeOfWork,
        CancellationToken cancellationToken)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var estimate = await db.Estimates
            .Include(e => e.Lines)
            .Include(e => e.Project).ThenInclude(p => p!.Customer)
            .FirstOrDefaultAsync(e => e.Id == estimateId, cancellationToken)
            ?? throw new NotFoundException(nameof(Estimate), estimateId);

        if (estimate.Status != EstimateStatus.Finalized)
        {
            throw new ConflictException(
                "Only a finalized estimate can be turned into a quote. Finalize it first.");
        }

        var project = estimate.Project ?? throw new NotFoundException("Project", estimate.ProjectId);
        var customer = project.Customer ?? throw new NotFoundException("Customer", project.CustomerId);

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            ?? throw new NotFoundException("Organization", organizationId);

        var settings = await db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("OrganizationSettings", organizationId);

        var now = clock.UtcNow;
        var number = await numbers.NextAsync(organizationId, "Quote", settings.QuoteNumberPrefix, cancellationToken);

        var quote = Quote.Create(organizationId, project.Id, estimate.Id, customer.Id,
            number, now, settings.QuoteValidityDays, estimate.Currency);

        quote.ApplyCustomerSnapshot(customer.DisplayName, customer.Email, customer.Phone,
            customer.ServiceAddress?.SingleLine ?? customer.BillingAddress?.SingleLine);

        quote.ApplyCompanySnapshot(organization.Name, organization.Email, organization.Phone,
            organization.Address?.SingleLine, organization.LicenseNumber, settings.LogoPath);

        quote.SetContent(
            title ?? estimate.Title ?? project.Name,
            scopeOfWork ?? BuildDefaultScope(project.Name, estimate),
            settings.QuoteTermsAndConditions,
            settings.QuoteFooterNote);

        quote.ApplyTotals(
            estimate.MaterialCost + estimate.MarkupAmount * ShareOf(estimate.MaterialCost, estimate),
            estimate.LaborCost + estimate.MarkupAmount * ShareOf(estimate.LaborCost, estimate),
            estimate.OtherCost + estimate.OverheadAmount,
            estimate.Subtotal,
            estimate.DiscountAmount,
            estimate.TaxAmount,
            estimate.GrandTotal);

        db.Quotes.Add(quote);

        var sortOrder = 0;
        var rooms = await db.Rooms
            .Where(r => r.ProjectId == project.Id)
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        foreach (var line in estimate.Lines.OrderBy(l => l.SortOrder))
        {
            var roomName = line.RoomId.HasValue && rooms.TryGetValue(line.RoomId.Value, out var name)
                ? name
                : null;

            var quoteLine = QuoteLine.Create(organizationId, quote.Id, line.Id, roomName,
                line.Category, line.Description,
                line.PurchaseQuantity > 0m ? line.PurchaseQuantity : line.Quantity,
                line.Unit, line.UnitPrice, sortOrder++);

            // Tracked through the DbSet; see the note in EstimateService about new entities
            // added through a navigation collection.
            db.QuoteLines.Add(quoteLine);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Refresh the navigation from what was actually written. EF's fixup has already put the
        // saved lines here, so appending them again would double the collection.
        var savedLines = await db.QuoteLines
            .Where(l => l.QuoteId == quote.Id)
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        quote.Lines.Clear();
        foreach (var line in savedLines)
        {
            quote.Lines.Add(line);
        }

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Quote), quote.Id.ToString(),
            $"Quote {quote.QuoteNumber} created from estimate {estimate.DisplayNumber}.", cancellationToken);

        return quote;
    }

    /// <summary>Renders the proposal PDF and stores it. Safe to call repeatedly.</summary>
    public async Task<byte[]> GeneratePdfAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await LoadForPdfAsync(quoteId, cancellationToken);
        return await GeneratePdfAsync(quote, cancellationToken);
    }

    private async Task<byte[]> GeneratePdfAsync(Quote quote, CancellationToken cancellationToken)
    {
        byte[]? logo = null;

        if (!string.IsNullOrWhiteSpace(quote.CompanyLogoPath))
        {
            await using var stream = await fileStorage.OpenAsync(quote.CompanyLogoPath, cancellationToken);
            if (stream is not null)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                logo = buffer.ToArray();
            }
        }

        var project = await db.Projects
            .FirstOrDefaultAsync(p => p.Id == quote.ProjectId, cancellationToken);

        var model = new QuotePdfModel
        {
            QuoteNumber = quote.QuoteNumber,
            QuoteDate = quote.QuoteDate,
            ExpiresAt = quote.ExpiresAt,
            Title = quote.Title,
            ScopeOfWork = quote.ScopeOfWork,
            TermsAndConditions = quote.TermsAndConditions,
            FooterNote = quote.FooterNote,
            CompanyName = quote.CompanyName,
            CompanyEmail = quote.CompanyEmail,
            CompanyPhone = quote.CompanyPhone,
            CompanyAddressLine = quote.CompanyAddressLine,
            CompanyLicenseNumber = quote.CompanyLicenseNumber,
            CompanyLogo = logo,
            CustomerName = quote.CustomerDisplayName,
            CustomerEmail = quote.CustomerEmail,
            CustomerPhone = quote.CustomerPhone,
            CustomerAddressLine = quote.CustomerAddressLine,
            ProjectName = project?.Name,
            ProjectAddressLine = project?.SiteAddress?.SingleLine,
            MaterialTotal = quote.MaterialTotal,
            LaborTotal = quote.LaborTotal,
            OtherTotal = quote.OtherTotal,
            Subtotal = quote.Subtotal,
            DiscountAmount = quote.DiscountAmount,
            TaxAmount = quote.TaxAmount,
            GrandTotal = quote.GrandTotal,
            Lines = quote.Lines
                .Where(l => l.IsVisibleToCustomer)
                .OrderBy(l => l.SortOrder)
                .Select(l => new QuotePdfLine(l.RoomName, l.Category.ToString(), l.Description,
                    l.Quantity, l.Unit, l.UnitPrice, l.TotalPrice))
                .ToList()
        };

        var pdf = pdfGenerator.Generate(model);

        using var pdfStream = new MemoryStream(pdf);
        var storageKey = await fileStorage.SaveAsync("quotes", $"Quote-{quote.QuoteNumber}.pdf",
            pdfStream, "application/pdf", cancellationToken);

        quote.SetPdf(storageKey);
        await db.SaveChangesAsync(cancellationToken);

        return pdf;
    }

    /// <summary>
    /// Sends the quote and issues a fresh public link. Re-sending always rotates the token, so
    /// a link forwarded from an earlier send stops working.
    /// </summary>
    public async Task SendAsync(Guid quoteId, IReadOnlyList<string> recipientEmails, string? message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientEmails);

        var organizationId = currentOrganization.RequireOrganizationId();
        var quote = await LoadForPdfAsync(quoteId, cancellationToken);

        var emails = recipientEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (emails.Count == 0 && !string.IsNullOrWhiteSpace(quote.CustomerEmail))
        {
            emails.Add(quote.CustomerEmail);
        }

        if (emails.Count == 0)
        {
            throw new ValidationFailedException("recipients",
                "This quote has no recipient. Add an email address for the customer.");
        }

        var pdf = await GeneratePdfAsync(quote, cancellationToken);

        var token = SecureToken.Generate();
        var tokenExpiry = quote.ExpiresAt.AddDays(options.PublicLinkGraceDays);
        quote.MarkSent(SecureToken.Hash(token), clock.UtcNow, tokenExpiry);

        var link = $"{options.WebAppBaseUrl.TrimEnd('/')}/quote/{Uri.EscapeDataString(token)}";

        foreach (var email in emails)
        {
            var recipient = QuoteRecipient.Create(organizationId, quote.Id, email, null);
            db.QuoteRecipients.Add(recipient);

            var result = await deliveryProvider.DeliverAsync(new QuoteDeliveryRequest(
                quote.Id, quote.QuoteNumber, email, null, quote.CompanyName, link, message, pdf),
                cancellationToken);

            if (result.Succeeded)
            {
                recipient.MarkSent(clock.UtcNow);
            }
            else
            {
                recipient.MarkFailed(result.Error ?? "Delivery failed.");
                logger.LogWarning("Quote {QuoteNumber} could not be delivered to a recipient.", quote.QuoteNumber);
            }
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == quote.ProjectId, cancellationToken);
        if (project is not null && project.Status == ProjectStatus.EstimateReady)
        {
            project.ChangeStatus(ProjectStatus.Quoted);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.RecordAsync(organizationId, AuditAction.Send, nameof(Quote), quote.Id.ToString(),
            $"Quote {quote.QuoteNumber} sent to {emails.Count} recipient(s).", cancellationToken);
    }

    /// <summary>
    /// Resolves a public quote link. Runs with tenant filtering suspended because the customer
    /// has no account and no organization context; the token is the only credential, and it is
    /// matched against a stored hash.
    /// </summary>
    public async Task<Quote?> GetByPublicTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var bypass = currentOrganization.BypassTenantFilter();

        var hash = SecureToken.Hash(token);
        var now = clock.UtcNow;

        var quote = await db.Quotes
            .IgnoreQueryFilters()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.PublicTokenHash == hash && !q.IsDeleted, cancellationToken);

        if (quote is null || !quote.IsPublicLinkValid(now))
        {
            return null;
        }

        // Cancelled and already-answered quotes stay viewable, so the customer can see what
        // they agreed to, but they are no longer open for a new response.
        return quote;
    }

    /// <summary>Records the first time a customer opened the quote (SPEC 17).</summary>
    public async Task MarkViewedAsync(Quote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        using var bypass = currentOrganization.BypassTenantFilter();

        if (quote.FirstViewedAt is not null && quote.Status != QuoteStatus.Sent)
        {
            return;
        }

        quote.MarkViewed(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records the customer's decision from the public page, together with when it happened and
    /// where from. This is an approval record, not an electronic signature.
    /// </summary>
    public async Task<QuoteApproval> RecordDecisionAsync(Quote quote, QuoteApprovalDecision decision,
        string customerName, string? customerEmail, string? comments, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        using var bypass = currentOrganization.BypassTenantFilter();

        var now = clock.UtcNow;

        switch (decision)
        {
            case QuoteApprovalDecision.Accepted:
                quote.MarkAccepted(now);
                break;
            case QuoteApprovalDecision.Rejected:
                quote.MarkRejected(now);
                break;
            case QuoteApprovalDecision.ChangesRequested:
                quote.MarkChangesRequested(now);
                break;
            default:
                throw new ValidationFailedException(nameof(decision), "That is not a valid response.");
        }

        var approval = QuoteApproval.Create(quote.OrganizationId, quote.Id, decision, customerName,
            customerEmail, comments, now, ipAddress, userAgent);
        db.QuoteApprovals.Add(approval);

        if (decision == QuoteApprovalDecision.Accepted)
        {
            var project = await db.Projects.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == quote.ProjectId, cancellationToken);
            project?.ChangeStatus(ProjectStatus.Accepted);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.RecordAsync(quote.OrganizationId,
            decision == QuoteApprovalDecision.Rejected ? AuditAction.Reject : AuditAction.Approve,
            nameof(Quote), quote.Id.ToString(),
            $"Customer {customerName} responded to quote {quote.QuoteNumber}: {decision}.",
            cancellationToken);

        return approval;
    }

    /// <summary>
    /// Expires sent quotes whose date has passed. Queued through the background job abstraction
    /// rather than run inside a request.
    /// </summary>
    public async Task<int> ExpireOverdueQuotesAsync(CancellationToken cancellationToken)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        var now = clock.UtcNow;

        var overdue = await db.Quotes
            .IgnoreQueryFilters()
            .Where(q => !q.IsDeleted
                        && (q.Status == QuoteStatus.Sent || q.Status == QuoteStatus.Viewed)
                        && q.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var quote in overdue)
        {
            quote.MarkExpired();
        }

        if (overdue.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Expired {Count} overdue quote(s).", overdue.Count);
        }

        return overdue.Count;
    }

    private async Task<Quote> LoadForPdfAsync(Guid quoteId, CancellationToken cancellationToken) =>
        await db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
        ?? throw new NotFoundException(nameof(Quote), quoteId);

    private static string BuildDefaultScope(string projectName, Estimate estimate)
    {
        var rooms = estimate.Lines
            .Select(l => l.Description)
            .Take(6)
            .ToList();

        return $"Supply and installation for {projectName}, including: "
               + string.Join(", ", rooms)
               + (estimate.Lines.Count > rooms.Count ? ", and related work." : ".");
    }

    /// <summary>
    /// Splits the markup across material and labor in proportion to their cost, so the quote's
    /// category totals add up to the same grand total the estimate produced.
    /// </summary>
    private static decimal ShareOf(decimal component, Estimate estimate)
    {
        var basis = estimate.MaterialCost + estimate.LaborCost + estimate.OtherCost;
        return basis == 0m ? 0m : component / basis;
    }
}
