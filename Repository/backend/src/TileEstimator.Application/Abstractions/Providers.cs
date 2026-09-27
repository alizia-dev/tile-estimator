namespace TileEstimator.Application.Abstractions;

/// <summary>Stores and retrieves files. Local disk in the MVP, Blob Storage later (SPEC 20).</summary>
public interface IFileStorageProvider
{
    Task<string> SaveAsync(string container, string fileName, Stream content, string contentType,
        CancellationToken cancellationToken);

    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Sends email. The development implementation writes messages to disk so the whole flow can be
/// exercised without an SMTP server or an external account (SPEC 20).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>In-app and push notifications.</summary>
public interface INotificationProvider
{
    Task NotifyAsync(Guid organizationId, Guid userId, string title, string body, string? link,
        CancellationToken cancellationToken);
}

/// <summary>Delivers a quote to a customer, PDF attached (SPEC 17).</summary>
public interface IQuoteDeliveryProvider
{
    Task<QuoteDeliveryResult> DeliverAsync(QuoteDeliveryRequest request, CancellationToken cancellationToken);
}

public sealed record QuoteDeliveryRequest(
    Guid QuoteId,
    string QuoteNumber,
    string RecipientEmail,
    string? RecipientName,
    string CompanyName,
    string PublicLinkUrl,
    string? Message,
    byte[] PdfContent);

public sealed record QuoteDeliveryResult(bool Succeeded, string? Error = null);

/// <summary>Queues work that should not block the request, e.g. expiring stale quotes.</summary>
public interface IBackgroundJobScheduler
{
    void Enqueue(Func<IServiceProviderAccessor, CancellationToken, Task> work, string description);
}

/// <summary>Hands background work a scope to resolve services from.</summary>
public interface IServiceProviderAccessor
{
    T GetRequiredService<T>() where T : notnull;
}

/// <summary>Renders a quote as a PDF proposal (SPEC 17).</summary>
public interface IQuotePdfGenerator
{
    byte[] Generate(QuotePdfModel model);
}

public sealed record QuotePdfModel
{
    public required string QuoteNumber { get; init; }
    public required DateTime QuoteDate { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public string? Title { get; init; }
    public string? ScopeOfWork { get; init; }
    public string? TermsAndConditions { get; init; }
    public string? FooterNote { get; init; }

    public required string CompanyName { get; init; }
    public string? CompanyEmail { get; init; }
    public string? CompanyPhone { get; init; }
    public string? CompanyAddressLine { get; init; }
    public string? CompanyLicenseNumber { get; init; }
    public byte[]? CompanyLogo { get; init; }

    public required string CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerAddressLine { get; init; }

    public string? ProjectName { get; init; }
    public string? ProjectAddressLine { get; init; }

    public IReadOnlyList<QuotePdfLine> Lines { get; init; } = [];
    public decimal MaterialTotal { get; init; }
    public decimal LaborTotal { get; init; }
    public decimal OtherTotal { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal GrandTotal { get; init; }
}

public sealed record QuotePdfLine(
    string? RoomName,
    string Category,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalPrice);
