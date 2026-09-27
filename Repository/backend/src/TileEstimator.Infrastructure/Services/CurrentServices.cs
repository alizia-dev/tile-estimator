using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;

namespace TileEstimator.Infrastructure.Services;

public sealed class SystemClock : IDateTimeProvider
{
    /// <summary>UTC everywhere. Local time never enters the domain.</summary>
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Holds the tenant context for one request. The API populates it from the caller's verified
/// membership; nothing else may set it, which is what keeps SPEC 4 honest.
/// </summary>
public sealed class CurrentOrganizationService : ICurrentOrganizationService
{
    private static readonly HashSet<string> NoPermissions = new(StringComparer.Ordinal);

    private Guid? _organizationId;
    private HashSet<string> _permissions = NoPermissions;
    private int _bypassDepth;

    public Guid? OrganizationId => _organizationId;

    public IReadOnlySet<string> Permissions => _permissions;

    public bool IsTenantFilterBypassed => _bypassDepth > 0;

    public Guid RequireOrganizationId() =>
        _organizationId ?? throw new ForbiddenException("This request has no active organization.");

    public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);

    /// <summary>
    /// Called once per request by the tenant middleware, after membership has been verified.
    /// </summary>
    public void Set(Guid? organizationId, IEnumerable<string> permissions)
    {
        _organizationId = organizationId;
        _permissions = permissions is null
            ? NoPermissions
            : new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    public IDisposable BypassTenantFilter()
    {
        _bypassDepth++;
        return new BypassScope(this);
    }

    private void EndBypass()
    {
        if (_bypassDepth > 0)
        {
            _bypassDepth--;
        }
    }

    /// <summary>Nested bypasses are counted, so an inner scope cannot end an outer one early.</summary>
    private sealed class BypassScope(CurrentOrganizationService owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.EndBypass();
        }
    }
}
