using System.ComponentModel.DataAnnotations;
using TileEstimator.Contracts.Common;

namespace TileEstimator.Contracts.Quoting;

public sealed record QuoteResponse(
    Guid Id,
    string QuoteNumber,
    int Version,
    string Status,
    Guid ProjectId,
    string ProjectName,
    Guid EstimateId,
    string EstimateNumber,
    Guid CustomerId,
    string CustomerDisplayName,
    string? CustomerEmail,
    string? Title,
    string? ScopeOfWork,
    string? TermsAndConditions,
    string Currency,
    DateTime QuoteDate,
    DateTime ExpiresAt,
    decimal MaterialTotal,
    decimal LaborTotal,
    decimal OtherTotal,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    DateTime? SentAt,
    DateTime? FirstViewedAt,
    DateTime? RespondedAt,
    bool HasPdf,
    IReadOnlyList<QuoteLineResponse> Lines,
    IReadOnlyList<QuoteRecipientResponse> Recipients,
    IReadOnlyList<QuoteApprovalResponse> Approvals);

public sealed record QuoteLineResponse(
    Guid Id,
    string? RoomName,
    string Category,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalPrice,
    bool IsVisibleToCustomer,
    int SortOrder);

public sealed record QuoteRecipientResponse(
    Guid Id, string Email, string? Name, DateTime? SentAt, string? DeliveryError);

public sealed record QuoteApprovalResponse(
    Guid Id, string Decision, string CustomerName, string? CustomerEmail,
    string? Comments, DateTime DecidedAt);

public sealed record CreateQuoteRequest
{
    [Required]
    public Guid EstimateId { get; init; }

    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(8000)]
    public string? ScopeOfWork { get; init; }
}

public sealed record SendQuoteRequest
{
    /// <summary>Leave empty to send to the customer's own email address.</summary>
    public IReadOnlyList<string> RecipientEmails { get; init; } = [];

    [MaxLength(2000)]
    public string? Message { get; init; }
}

public sealed record UpdateQuoteContentRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(8000)]
    public string? ScopeOfWork { get; init; }

    [MaxLength(8000)]
    public string? TermsAndConditions { get; init; }

    [MaxLength(1000)]
    public string? FooterNote { get; init; }
}

public sealed record QuoteQuery : PagedRequest
{
    public string? Status { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? CustomerId { get; init; }
}

public sealed record QuoteSummaryResponse(
    Guid Id,
    string QuoteNumber,
    string Status,
    Guid ProjectId,
    string ProjectName,
    string CustomerDisplayName,
    decimal GrandTotal,
    string Currency,
    DateTime QuoteDate,
    DateTime ExpiresAt,
    DateTime? SentAt,
    DateTime? RespondedAt);

// --- Public, unauthenticated customer view (SPEC 17) ----------------------------------------------

/// <summary>
/// What the customer sees on the public quote page. Deliberately narrow: no cost, no margin,
/// no ids that would let the page be used to reach anything else.
/// </summary>
public sealed record PublicQuoteResponse(
    string QuoteNumber,
    string Status,
    string? Title,
    string? ScopeOfWork,
    string? TermsAndConditions,
    string? FooterNote,
    string CompanyName,
    string? CompanyEmail,
    string? CompanyPhone,
    string? CompanyAddressLine,
    string? CompanyLicenseNumber,
    string CustomerName,
    string? ProjectName,
    string Currency,
    DateTime QuoteDate,
    DateTime ExpiresAt,
    bool IsExpired,
    bool CanRespond,
    decimal MaterialTotal,
    decimal LaborTotal,
    decimal OtherTotal,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    IReadOnlyList<PublicQuoteLineResponse> Lines,
    PublicQuoteDecisionResponse? ExistingDecision);

public sealed record PublicQuoteLineResponse(
    string? RoomName, string Description, decimal Quantity, string Unit,
    decimal UnitPrice, decimal TotalPrice);

public sealed record PublicQuoteDecisionResponse(string Decision, string CustomerName, DateTime DecidedAt);

public sealed record PublicQuoteDecisionRequest
{
    /// <summary>Accepted, Rejected or ChangesRequested.</summary>
    [Required]
    public string Decision { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string CustomerName { get; init; } = string.Empty;

    [EmailAddress, MaxLength(256)]
    public string? CustomerEmail { get; init; }

    [MaxLength(4000)]
    public string? Comments { get; init; }
}

// --- Change orders (modelled now, full workflow in V2) ----------------------------------------------

public sealed record ChangeOrderResponse(
    Guid Id,
    Guid ProjectId,
    Guid? QuoteId,
    string Number,
    string Description,
    string? Reason,
    decimal Amount,
    string Status,
    DateTime? ApprovedAt,
    DateTime CreatedAt);

public sealed record SaveChangeOrderRequest
{
    [Required]
    public Guid ProjectId { get; init; }

    public Guid? QuoteId { get; init; }

    [Required, MaxLength(4000)]
    public string Description { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Reason { get; init; }

    public decimal Amount { get; init; }
}
