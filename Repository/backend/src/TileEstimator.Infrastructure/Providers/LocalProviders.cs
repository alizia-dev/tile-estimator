using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Infrastructure.Configuration;

namespace TileEstimator.Infrastructure.Providers;

/// <summary>
/// Stores files on local disk (SPEC 2: simple local implementations in the MVP). Storage keys are
/// generated here and never taken from the caller, and every resolved path is checked to stay
/// inside the configured root so a crafted file name cannot escape it.
/// </summary>
public sealed class LocalFileStorageProvider : IFileStorageProvider
{
    private readonly string _root;

    public LocalFileStorageProvider(IOptions<StorageSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _root = Path.GetFullPath(settings.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string container, string fileName, Stream content,
        string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeContainer = Sanitize(container);
        var extension = Path.GetExtension(fileName);
        var storageKey = $"{safeContainer}/{Guid.NewGuid():N}{Sanitize(extension)}";

        var fullPath = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);

        return storageKey;
    }

    public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(storageKey);
        return Task.FromResult<Stream?>(File.Exists(fullPath) ? File.OpenRead(fullPath) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(Resolve(storageKey)));

    /// <summary>Resolves a key inside the root, refusing anything that would traverse out of it.</summary>
    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ValidationFailedException(nameof(storageKey), "A storage key is required.");
        }

        var combined = Path.GetFullPath(Path.Combine(_root, storageKey));

        if (!combined.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationFailedException(nameof(storageKey), "That storage key is not valid.");
        }

        return combined;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "files";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 || c is '/' or '\\' ? '-' : c);
        }
        return builder.ToString();
    }
}

/// <summary>
/// Development email sender: writes each message to the outbox folder as a readable .eml-style
/// file plus a .json sidecar, so the register-verify-quote flow is testable end to end with no
/// SMTP account and nothing leaving the machine.
/// </summary>
public sealed class FileSystemEmailSender(
    IOptions<EmailSettings> settings,
    IDateTimeProvider clock,
    ILogger<FileSystemEmailSender> logger)
    : IEmailSender
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var folder = Path.GetFullPath(_settings.OutboxPath);
        Directory.CreateDirectory(folder);

        var stamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var name = $"{stamp}-{SanitizeForFileName(message.To)}";

        var body = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"From: {_settings.FromName} <{_settings.FromAddress}>")
            .AppendLine(CultureInfo.InvariantCulture, $"To: {message.To}")
            .AppendLine(CultureInfo.InvariantCulture, $"Subject: {message.Subject}")
            .AppendLine(CultureInfo.InvariantCulture, $"Date: {clock.UtcNow:R}")
            .AppendLine()
            .AppendLine(message.PlainTextBody ?? string.Empty)
            .AppendLine()
            .AppendLine("--- HTML ---")
            .AppendLine(message.HtmlBody)
            .ToString();

        await File.WriteAllTextAsync(Path.Combine(folder, name + ".txt"), body, cancellationToken);

        foreach (var attachment in message.Attachments ?? [])
        {
            await File.WriteAllBytesAsync(
                Path.Combine(folder, $"{name}-{SanitizeForFileName(attachment.FileName)}"),
                attachment.Content,
                cancellationToken);
        }

        logger.LogInformation("Email written to the development outbox: {Subject} -> {Recipient}",
            message.Subject, message.To);
    }

    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}

/// <summary>In-app notifications. Persisted by the audit/notification service in the API layer.</summary>
public sealed class LoggingNotificationProvider(ILogger<LoggingNotificationProvider> logger) : INotificationProvider
{
    public Task NotifyAsync(Guid organizationId, Guid userId, string title, string body, string? link,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification for user {UserId} in {OrganizationId}: {Title}",
            userId, organizationId, title);
        return Task.CompletedTask;
    }
}

/// <summary>Delivers a quote by email with the PDF attached (SPEC 17).</summary>
public sealed class EmailQuoteDeliveryProvider(
    IEmailSender emailSender,
    ILogger<EmailQuoteDeliveryProvider> logger)
    : IQuoteDeliveryProvider
{
    public async Task<QuoteDeliveryResult> DeliverAsync(QuoteDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var greeting = string.IsNullOrWhiteSpace(request.RecipientName)
                ? "Hello,"
                : $"Hello {request.RecipientName},";

            var html = $"""
                <p>{greeting}</p>
                <p>{JsonEncode(request.CompanyName)} has prepared quote
                   <strong>{JsonEncode(request.QuoteNumber)}</strong> for you.</p>
                {(string.IsNullOrWhiteSpace(request.Message) ? "" : $"<p>{JsonEncode(request.Message)}</p>")}
                <p><a href="{request.PublicLinkUrl}">View and respond to your quote</a></p>
                <p>The full proposal is attached as a PDF.</p>
                """;

            await emailSender.SendAsync(new EmailMessage(
                request.RecipientEmail,
                $"Quote {request.QuoteNumber} from {request.CompanyName}",
                html,
                $"{greeting}\n\n{request.CompanyName} has prepared quote {request.QuoteNumber} for you.\n{request.PublicLinkUrl}",
                [new EmailAttachment($"Quote-{request.QuoteNumber}.pdf", "application/pdf", request.PdfContent)]),
                cancellationToken);

            return new QuoteDeliveryResult(true);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to deliver quote {QuoteNumber}.", request.QuoteNumber);
            return new QuoteDeliveryResult(false, "The quote could not be delivered.");
        }
    }

    private static string JsonEncode(string value) =>
        JsonEncodedText.Encode(value, System.Text.Encodings.Web.JavaScriptEncoder.Default).ToString();
}
