using TileEstimator.Domain.Common;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Quotes;

/// <summary>
/// SPEC 17 quote: what the customer sees and approves. A quote is a snapshot of a finalized
/// estimate. It copies the customer and company details it was sent with, so re-printing it
/// years later reproduces the same document even if the catalog and the org profile have moved on.
/// </summary>
public class Quote : TenantEntity, IHasRowVersion
{
    public Guid ProjectId { get; private set; }
    public Guid EstimateId { get; private set; }
    public Guid CustomerId { get; private set; }

    public string QuoteNumber { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;
    public QuoteStatus Status { get; private set; } = QuoteStatus.Draft;

    public string? Title { get; private set; }
    public string? ScopeOfWork { get; private set; }
    public string? TermsAndConditions { get; private set; }
    public string? FooterNote { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateTime QuoteDate { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    // --- Snapshot of the estimate totals at conversion time ---
    public decimal MaterialTotal { get; private set; }
    public decimal LaborTotal { get; private set; }
    public decimal OtherTotal { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrandTotal { get; private set; }

    // --- Snapshot of who it was addressed to and from ---
    public string CustomerDisplayName { get; private set; } = string.Empty;
    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? CustomerAddressLine { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string? CompanyEmail { get; private set; }
    public string? CompanyPhone { get; private set; }
    public string? CompanyAddressLine { get; private set; }
    public string? CompanyLicenseNumber { get; private set; }
    public string? CompanyLogoPath { get; private set; }

    /// <summary>SHA-256 of the public link token. The token itself is only ever sent to the customer.</summary>
    public string? PublicTokenHash { get; private set; }
    public DateTime? PublicTokenExpiresAt { get; private set; }

    public DateTime? SentAt { get; private set; }
    public DateTime? FirstViewedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public string? PdfStorageKey { get; private set; }

    public byte[]? RowVersion { get; set; }

    public Project? Project { get; private set; }
    public Estimate? Estimate { get; private set; }
    public Customer? Customer { get; private set; }
    public ICollection<QuoteLine> Lines { get; private set; } = new List<QuoteLine>();
    public ICollection<QuoteRecipient> Recipients { get; private set; } = new List<QuoteRecipient>();
    public ICollection<QuoteApproval> Approvals { get; private set; } = new List<QuoteApproval>();

    private Quote() { }

    public static Quote Create(Guid organizationId, Guid projectId, Guid estimateId, Guid customerId,
        string quoteNumber, DateTime quoteDate, int validityDays, string currency)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(quoteNumber), "Quote number is required.");
        DomainException.Require(validityDays > 0, "A quote must be valid for at least one day.");

        return new Quote
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            EstimateId = estimateId,
            CustomerId = customerId,
            QuoteNumber = quoteNumber,
            QuoteDate = quoteDate,
            ExpiresAt = quoteDate.AddDays(validityDays),
            Currency = currency
        };
    }

    public void ApplyTotals(decimal materialTotal, decimal laborTotal, decimal otherTotal,
        decimal subtotal, decimal discountAmount, decimal taxAmount, decimal grandTotal)
    {
        EnsureDraft();
        MaterialTotal = Rounding.Money(materialTotal);
        LaborTotal = Rounding.Money(laborTotal);
        OtherTotal = Rounding.Money(otherTotal);
        Subtotal = Rounding.Money(subtotal);
        DiscountAmount = Rounding.Money(discountAmount);
        TaxAmount = Rounding.Money(taxAmount);
        GrandTotal = Rounding.Money(grandTotal);
    }

    public void ApplyCustomerSnapshot(string displayName, string? email, string? phone, string? addressLine)
    {
        CustomerDisplayName = displayName;
        CustomerEmail = email;
        CustomerPhone = phone;
        CustomerAddressLine = addressLine;
    }

    public void ApplyCompanySnapshot(string companyName, string? email, string? phone, string? addressLine,
        string? licenseNumber, string? logoPath)
    {
        CompanyName = companyName;
        CompanyEmail = email;
        CompanyPhone = phone;
        CompanyAddressLine = addressLine;
        CompanyLicenseNumber = licenseNumber;
        CompanyLogoPath = logoPath;
    }

    public void SetContent(string? title, string? scopeOfWork, string? terms, string? footerNote)
    {
        EnsureDraft();
        Title = title;
        ScopeOfWork = scopeOfWork;
        TermsAndConditions = terms;
        FooterNote = footerNote;
    }

    public void AddLine(QuoteLine line)
    {
        EnsureDraft();
        Lines.Add(line);
    }

    public void SetPdf(string storageKey) => PdfStorageKey = storageKey;

    /// <summary>Marks the quote sent and arms the public link. Re-sending issues a fresh token.</summary>
    public void MarkSent(string publicTokenHash, DateTime utcNow, DateTime tokenExpiresAt)
    {
        DomainException.Require(Status is QuoteStatus.Draft or QuoteStatus.Sent or QuoteStatus.Viewed,
            "Only a draft or already-sent quote can be sent.");
        DomainException.Require(Lines.Count > 0, "A quote needs at least one line before it can be sent.");

        PublicTokenHash = publicTokenHash;
        PublicTokenExpiresAt = tokenExpiresAt;
        SentAt ??= utcNow;
        if (Status == QuoteStatus.Draft)
        {
            Status = QuoteStatus.Sent;
        }
    }

    /// <summary>First open by the customer moves Sent to Viewed. Later opens change nothing.</summary>
    public void MarkViewed(DateTime utcNow)
    {
        FirstViewedAt ??= utcNow;
        if (Status == QuoteStatus.Sent)
        {
            Status = QuoteStatus.Viewed;
        }
    }

    public void MarkAccepted(DateTime utcNow)
    {
        EnsureRespondable(utcNow);
        Status = QuoteStatus.Accepted;
        RespondedAt = utcNow;
    }

    public void MarkRejected(DateTime utcNow)
    {
        EnsureRespondable(utcNow);
        Status = QuoteStatus.Rejected;
        RespondedAt = utcNow;
    }

    /// <summary>A change request keeps the quote open: the contractor follows up and re-sends.</summary>
    public void MarkChangesRequested(DateTime utcNow)
    {
        EnsureRespondable(utcNow);
        RespondedAt = utcNow;
    }

    public void MarkExpired()
    {
        DomainException.Require(Status is QuoteStatus.Sent or QuoteStatus.Viewed,
            "Only a sent or viewed quote can expire.");
        Status = QuoteStatus.Expired;
    }

    public void Cancel()
    {
        DomainException.Require(Status is not (QuoteStatus.Accepted or QuoteStatus.Rejected),
            "A quote the customer has already answered cannot be cancelled.");
        Status = QuoteStatus.Cancelled;
    }

    public bool IsPublicLinkValid(DateTime utcNow) =>
        PublicTokenHash is not null && PublicTokenExpiresAt.HasValue && utcNow < PublicTokenExpiresAt.Value;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    private void EnsureDraft() =>
        DomainException.Require(Status == QuoteStatus.Draft, "A sent quote is a snapshot and cannot be edited.");

    private void EnsureRespondable(DateTime utcNow)
    {
        DomainException.Require(Status is QuoteStatus.Sent or QuoteStatus.Viewed,
            "This quote is not open for a response.");
        DomainException.Require(!IsExpired(utcNow), "This quote has expired.");
    }
}
