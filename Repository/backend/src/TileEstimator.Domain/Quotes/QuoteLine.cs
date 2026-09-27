using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Quotes;

/// <summary>
/// A priced line on the customer-facing quote, copied from the estimate at conversion time.
/// Cost is deliberately absent: the customer sees price, never the contractor's margin.
/// </summary>
public class QuoteLine : TenantEntity
{
    public Guid QuoteId { get; private set; }
    public Guid? EstimateLineId { get; private set; }
    public string? RoomName { get; private set; }
    public EstimateLineCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = UnitOfMeasure.Each;
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }

    /// <summary>Hidden lines roll into the total but are not itemised on the proposal.</summary>
    public bool IsVisibleToCustomer { get; private set; } = true;

    public int SortOrder { get; private set; }

    public Quote? Quote { get; private set; }

    private QuoteLine() { }

    public static QuoteLine Create(Guid organizationId, Guid quoteId, Guid? estimateLineId, string? roomName,
        EstimateLineCategory category, string description, decimal quantity, string unit,
        decimal unitPrice, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(description), "Quote line description is required.");
        DomainException.Require(quantity >= 0m, "Quantity cannot be negative.");
        DomainException.Require(unitPrice >= 0m, "Unit price cannot be negative.");

        return new QuoteLine
        {
            OrganizationId = organizationId,
            QuoteId = quoteId,
            EstimateLineId = estimateLineId,
            RoomName = roomName,
            Category = category,
            Description = description.Trim(),
            Quantity = Rounding.Quantity(quantity),
            Unit = unit,
            UnitPrice = Rounding.Money(unitPrice),
            TotalPrice = Rounding.Money(unitPrice * quantity),
            SortOrder = sortOrder
        };
    }

    public void SetVisibility(bool visible) => IsVisibleToCustomer = visible;
}

/// <summary>Someone the quote was emailed to. One quote can go to several people.</summary>
public class QuoteRecipient : TenantEntity
{
    public Guid QuoteId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? DeliveryError { get; private set; }

    public Quote? Quote { get; private set; }

    private QuoteRecipient() { }

    public static QuoteRecipient Create(Guid organizationId, Guid quoteId, string email, string? name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(email), "Recipient email is required.");
        DomainException.Require(email.Contains('@'), "Recipient email is not valid.");

        return new QuoteRecipient
        {
            OrganizationId = organizationId,
            QuoteId = quoteId,
            Email = email.Trim(),
            Name = name?.Trim()
        };
    }

    public void MarkSent(DateTime utcNow)
    {
        SentAt = utcNow;
        DeliveryError = null;
    }

    public void MarkFailed(string error) => DeliveryError = error;
}

/// <summary>
/// SPEC 17 record of what the customer did on the public quote page. This is an approval
/// record, not an e-signature: nothing here is presented as legally binding.
/// </summary>
public class QuoteApproval : TenantEntity
{
    public Guid QuoteId { get; private set; }
    public QuoteApprovalDecision Decision { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string? CustomerEmail { get; private set; }
    public string? Comments { get; private set; }
    public DateTime DecidedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public Quote? Quote { get; private set; }

    private QuoteApproval() { }

    public static QuoteApproval Create(Guid organizationId, Guid quoteId, QuoteApprovalDecision decision,
        string customerName, string? customerEmail, string? comments, DateTime utcNow,
        string? ipAddress, string? userAgent)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(customerName),
            "We record who responded, so a name is required.");

        return new QuoteApproval
        {
            OrganizationId = organizationId,
            QuoteId = quoteId,
            Decision = decision,
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail?.Trim(),
            Comments = comments,
            DecidedAt = utcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}

/// <summary>
/// SPEC 17 change order. Modelled now so the data is there; the full approval workflow is V2.
/// </summary>
public class ChangeOrder : TenantEntity, IHasRowVersion
{
    public Guid ProjectId { get; private set; }
    public Guid? QuoteId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public decimal Amount { get; private set; }
    public ChangeOrderStatus Status { get; private set; } = ChangeOrderStatus.Draft;
    public DateTime? ApprovedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }

    public byte[]? RowVersion { get; set; }

    private ChangeOrder() { }

    public static ChangeOrder Create(Guid organizationId, Guid projectId, Guid? quoteId, string number,
        string description, string? reason, decimal amount)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(description), "Change order description is required.");
        return new ChangeOrder
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            QuoteId = quoteId,
            Number = number,
            Description = description.Trim(),
            Reason = reason,
            Amount = Rounding.Money(amount)
        };
    }

    public void Update(string description, string? reason, decimal amount)
    {
        DomainException.Require(Status == ChangeOrderStatus.Draft, "Only a draft change order can be edited.");
        DomainException.Require(!string.IsNullOrWhiteSpace(description), "Change order description is required.");
        Description = description.Trim();
        Reason = reason;
        Amount = Rounding.Money(amount);
    }

    public void ChangeStatus(ChangeOrderStatus status, Guid? userId, DateTime utcNow)
    {
        Status = status;
        if (status == ChangeOrderStatus.Approved)
        {
            ApprovedAt = utcNow;
            ApprovedBy = userId;
        }
    }
}
