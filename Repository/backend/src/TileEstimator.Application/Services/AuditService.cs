using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Application.Abstractions;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.SystemModule;

namespace TileEstimator.Application.Services;

/// <summary>SPEC 18 audit trail. Every audited action goes through here.</summary>
public interface IAuditService
{
    Task RecordAsync(Guid organizationId, AuditAction action, string entityName, string? entityId,
        string? summary, CancellationToken cancellationToken);

    /// <summary>Records a change with before and after values, with sensitive fields redacted.</summary>
    Task RecordChangeAsync(Guid organizationId, AuditAction action, string entityName, string? entityId,
        object? oldValues, object? newValues, string? summary, CancellationToken cancellationToken);
}

/// <summary>Provisions a new organization's default estimation configuration (SPEC 5).</summary>
public interface IOrganizationProvisioningService
{
    Task ProvisionDefaultsAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IAuditService"/>
public sealed class AuditService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
    : IAuditService
{
    private const int MaxSerializedLength = 8000;

    /// <summary>
    /// Field names that must never reach the audit table, whatever entity they appear on.
    /// Matching is case-insensitive and by substring, so PasswordHash and NewPassword both go.
    /// </summary>
    private static readonly string[] SensitiveFragments =
        ["password", "token", "secret", "hash", "apikey", "connectionstring", "credential"];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public Task RecordAsync(Guid organizationId, AuditAction action, string entityName, string? entityId,
        string? summary, CancellationToken cancellationToken) =>
        RecordChangeAsync(organizationId, action, entityName, entityId, null, null, summary, cancellationToken);

    public async Task RecordChangeAsync(Guid organizationId, AuditAction action, string entityName,
        string? entityId, object? oldValues, object? newValues, string? summary,
        CancellationToken cancellationToken)
    {
        var entry = AuditLog.Create(
            organizationId,
            currentUser.UserId,
            currentUser.Email,
            entityName,
            entityId,
            action,
            Serialize(oldValues),
            Serialize(newValues),
            Truncate(summary, 1000),
            currentUser.IpAddress,
            Truncate(currentUser.UserAgent, 512),
            currentUser.CorrelationId,
            clock.UtcNow);

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
        Redact(node);

        return Truncate(node?.ToJsonString(SerializerOptions), MaxSerializedLength);
    }

    /// <summary>Walks the serialized graph and replaces the value of any sensitive-looking field.</summary>
    private static void Redact(System.Text.Json.Nodes.JsonNode? node)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
            {
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = "[redacted]";
                    }
                    else
                    {
                        Redact(obj[key]);
                    }
                }
                break;
            }

            case System.Text.Json.Nodes.JsonArray array:
            {
                foreach (var item in array)
                {
                    Redact(item);
                }
                break;
            }
        }
    }

    private static bool IsSensitive(string name) =>
        SensitiveFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
