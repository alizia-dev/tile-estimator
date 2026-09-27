namespace TileEstimator.Application.Abstractions;

/// <summary>The authenticated caller, resolved from the JWT. Never from a request body.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
}

/// <summary>
/// The tenant the current request operates in (SPEC 4). The implementation resolves this from
/// the caller's verified membership, so an OrganizationId in a request body can never widen access.
/// </summary>
public interface ICurrentOrganizationService
{
    /// <summary>The active organization, or null for anonymous and public-quote requests.</summary>
    Guid? OrganizationId { get; }

    /// <summary>Throws when there is no active organization, so callers never silently query across tenants.</summary>
    Guid RequireOrganizationId();

    /// <summary>The permission codes the caller holds in the active organization.</summary>
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permissionCode);

    /// <summary>
    /// Suspends tenant filtering for a single unit of work. Used only by registration, the
    /// public quote endpoints and background jobs, which legitimately run outside a tenant.
    /// </summary>
    IDisposable BypassTenantFilter();

    bool IsTenantFilterBypassed { get; }
}

/// <summary>UTC clock, injected so time-dependent rules are testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Hashes and verifies passwords. Backed by the ASP.NET Core Identity hasher.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Returns whether the password matches, and whether the stored hash should be upgraded.</summary>
    (bool Succeeded, bool NeedsRehash) Verify(string hash, string password);
}

/// <summary>Issues and validates JWT access tokens (SPEC 5). Identity claims only, no business data.</summary>
public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, string email, Guid? organizationId, IEnumerable<string> permissions);
    DateTime GetAccessTokenExpiry();
}

/// <summary>
/// Allocates the next per-organization document number (CUST-1001, EST-1001 and so on).
/// The implementation takes a row lock so two concurrent requests cannot get the same number.
/// </summary>
public interface INumberSequenceService
{
    Task<string> NextAsync(Guid organizationId, string entityType, string prefix, CancellationToken cancellationToken);
}
