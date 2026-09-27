using System.Security.Claims;
using TileEstimator.Api.Middleware;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Services;
using TileEstimator.Infrastructure.Persistence.Seeding;

namespace TileEstimator.Api.Services;

/// <summary>Reads the caller's identity from the current request. Never from a request body.</summary>
public sealed class HttpCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();

    public string? CorrelationId =>
        accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string
        ?? accessor.HttpContext?.TraceIdentifier;
}

/// <summary>
/// Adapts the infrastructure provisioner to the Application-layer interface, so Application
/// can trigger provisioning during registration without depending on Infrastructure.
/// </summary>
public sealed class OrganizationProvisioningService(OrganizationProvisioner provisioner)
    : IOrganizationProvisioningService
{
    public Task ProvisionDefaultsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        provisioner.ProvisionDefaultsAsync(organizationId, cancellationToken);
}
