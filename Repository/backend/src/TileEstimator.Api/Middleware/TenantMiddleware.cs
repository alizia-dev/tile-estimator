using System.Security.Claims;
using TileEstimator.Application.Services.Auth;
using TileEstimator.Infrastructure.Services;

namespace TileEstimator.Api.Middleware;

/// <summary>Attaches a correlation id to every request and echoes it back on the response.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        // A client-supplied value is only trusted for tracing, and only when it is short and
        // plain, so it cannot be used to inject content into the logs.
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64 ||
            !correlationId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            correlationId = context.TraceIdentifier;
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}

/// <summary>
/// SPEC 4: resolves the active organization for the request and verifies membership.
/// <para>
/// The organization is taken from the JWT, or from the X-Organization-Id header when a user
/// belongs to several. Either way it is re-checked against the database on every request, so a
/// forged header or a stale token cannot reach another tenant's data.
/// </para>
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public const string OrganizationHeader = "X-Organization-Id";

    public async Task InvokeAsync(HttpContext context, CurrentOrganizationService currentOrganization,
        AuthService authService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentOrganization);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? context.User.FindFirstValue("sub");

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var requested = ResolveRequestedOrganization(context);

                var membership = await authService.ResolveMembershipAsync(
                    userId, requested, context.RequestAborted);

                if (membership is null)
                {
                    if (requested.HasValue)
                    {
                        logger.LogWarning(
                            "User {UserId} asked for organization {OrganizationId} without an active membership.",
                            userId, requested.Value);
                    }
                }
                else
                {
                    // Permissions are read from the database, not from the token, so a role
                    // change takes effect on the next request rather than at the next sign-in.
                    currentOrganization.Set(membership.Value.OrganizationId, membership.Value.Permissions);
                }
            }
        }

        await next(context);
    }

    private static Guid? ResolveRequestedOrganization(HttpContext context)
    {
        var header = context.Request.Headers[OrganizationHeader].FirstOrDefault();
        if (Guid.TryParse(header, out var fromHeader))
        {
            return fromHeader;
        }

        var claim = context.User.FindFirstValue("org");
        return Guid.TryParse(claim, out var fromClaim) ? fromClaim : null;
    }
}
