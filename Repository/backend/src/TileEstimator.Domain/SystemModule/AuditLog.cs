using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;

namespace TileEstimator.Domain.SystemModule;

/// <summary>
/// SPEC 18 audit entry. Append-only: audit rows are never updated and never deleted, not even
/// softly. Passwords, tokens and secrets are redacted before they ever reach this table.
/// </summary>
public class AuditLog : Entity, ITenantOwned
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? Summary { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(Guid organizationId, Guid? userId, string? userEmail, string entityName,
        string? entityId, AuditAction action, string? oldValues, string? newValues, string? summary,
        string? ipAddress, string? userAgent, string? correlationId, DateTime utcNow) => new()
        {
            OrganizationId = organizationId,
            UserId = userId,
            UserEmail = userEmail,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            Summary = summary,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            CreatedAt = utcNow
        };
}

/// <summary>An in-app notification for a user. Delivery goes through INotificationProvider.</summary>
public class Notification : TenantEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid organizationId, Guid userId, string title, string body, string? link)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(title), "Notification title is required.");
        return new Notification
        {
            OrganizationId = organizationId,
            UserId = userId,
            Title = title,
            Body = body,
            Link = link
        };
    }

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;
}

/// <summary>
/// A system-wide setting that is not tied to a tenant, for example the seed data version.
/// Anything an organization should control lives on OrganizationSettings instead.
/// </summary>
public class ApplicationSetting : Entity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ApplicationSetting() { }

    public static ApplicationSetting Create(string key, string value, string? description, DateTime utcNow)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(key), "Setting key is required.");
        return new ApplicationSetting
        {
            Key = key.Trim(),
            Value = value,
            Description = description,
            UpdatedAt = utcNow
        };
    }

    public void SetValue(string value, DateTime utcNow)
    {
        Value = value;
        UpdatedAt = utcNow;
    }
}
