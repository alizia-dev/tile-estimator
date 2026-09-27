using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Domain.Identity;

namespace TileEstimator.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the system-wide reference data every tenant shares: roles, permissions and the
/// role-to-permission matrix (SPEC 22). Idempotent, so it is safe to run on every startup:
/// it adds what is missing and reconciles the matrix without touching anything else.
/// </summary>
public sealed class SystemSeeder(
    ApplicationDbContext db,
    IDateTimeProvider clock,
    ILogger<SystemSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("System reference data seeded at {Timestamp}.", clock.UtcNow);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Permissions
            .Select(p => p.Code)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        foreach (var (code, group, description) in Permissions.All.Where(p => !existing.Contains(p.Code)))
        {
            // Deterministic ids keep the seed stable across environments and reruns.
            db.Permissions.Add(Permission.Create(DeterministicId("permission:" + code), code, group, description));
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles
            .Select(r => r.Name)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        foreach (var (name, description, sortOrder) in Roles.All.Where(r => !existing.Contains(r.Name)))
        {
            db.Roles.Add(Role.Create(DeterministicId("role:" + name), name, description, sortOrder));
        }
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id, StringComparer.Ordinal, cancellationToken);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.Ordinal, cancellationToken);
        var existing = await db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(cancellationToken);

        var existingPairs = existing.Select(e => (e.RoleId, e.PermissionId)).ToHashSet();

        foreach (var (roleName, permissionCodes) in Roles.Matrix)
        {
            if (!roles.TryGetValue(roleName, out var roleId))
            {
                continue;
            }

            foreach (var code in permissionCodes)
            {
                if (permissions.TryGetValue(code, out var permissionId) &&
                    existingPairs.Add((roleId, permissionId)))
                {
                    db.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
                }
            }
        }
    }

    /// <summary>
    /// Derives a stable GUID from a key so reference rows keep the same id in every database.
    /// </summary>
    internal static Guid DeterministicId(string key)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}
