using Microsoft.AspNetCore.Authorization;
using TileEstimator.Application.Abstractions;

namespace TileEstimator.Api.Authorization;

/// <summary>Requires a permission code, e.g. <c>[HasPermission(Permissions.EstimateUpdate)]</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicy.Prefix + permission);

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Checks the permission against the membership resolved for this request, not against the
/// claims in the token. A role or membership change therefore takes effect immediately instead
/// of lingering until the access token expires.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    ICurrentOrganizationService currentOrganization,
    ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (currentOrganization.OrganizationId is null)
        {
            logger.LogDebug("Permission {Permission} denied: the request has no active organization.",
                requirement.Permission);
            return Task.CompletedTask;
        }

        if (currentOrganization.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug("Permission {Permission} denied for organization {OrganizationId}.",
                requirement.Permission, currentOrganization.OrganizationId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Creates a policy on demand for each permission code, so new permissions do not have to be
/// registered one by one at startup.
/// </summary>
public sealed class PermissionPolicyProvider(
    Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionPolicy.Prefix.Length..];

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

public static class PermissionPolicy
{
    public const string Prefix = "perm:";
}
