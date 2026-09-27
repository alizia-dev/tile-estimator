using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services;
using TileEstimator.Application.Services.Auth;
using TileEstimator.Contracts.Auth;
using TileEstimator.Contracts.Common;
using TileEstimator.Contracts.Dashboard;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// Organization profile, pricing defaults, members and invitations (SPEC 5 and SPEC 6).
/// </summary>
[ApiController]
[Route("api/organization")]
[Produces("application/json")]
public sealed class OrganizationController(
    IApplicationDbContext db,
    ICurrentOrganizationService currentOrganization,
    IDateTimeProvider clock,
    IEmailSender emailSender,
    IAuditService audit,
    AuthOptions authOptions)
    : ControllerBase
{
    [HttpGet("settings")]
    [HasPermission(Permissions.SettingsRead)]
    [ProducesResponseType(typeof(OrganizationSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationSettingsResponse>> GetSettings(CancellationToken ct)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var organization = await db.Organizations
                               .Include(o => o.Settings)
                               .FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                           ?? throw new NotFoundException(nameof(Organization), organizationId);

        return Ok(ToResponse(organization));
    }

    [HttpPut("profile")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType(typeof(OrganizationSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationSettingsResponse>> UpdateProfile(
        UpdateOrganizationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var organization = await db.Organizations
                               .Include(o => o.Settings)
                               .FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                           ?? throw new NotFoundException(nameof(Organization), organizationId);

        organization.UpdateProfile(request.Name, request.LegalName, request.Email, request.Phone,
            request.Website, request.LicenseNumber, request.Address.ToDomain());

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Update, nameof(Organization),
            organizationId.ToString(), "Organization profile updated.", ct);

        return Ok(ToResponse(organization));
    }

    /// <summary>
    /// Updates the pricing and numbering defaults. These apply to estimates created afterwards;
    /// estimates already in progress keep the configuration they were created with.
    /// </summary>
    [HttpPut("settings")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType(typeof(OrganizationSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationSettingsResponse>> UpdateSettings(
        UpdateOrganizationSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var organization = await db.Organizations
                               .Include(o => o.Settings)
                               .FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                           ?? throw new NotFoundException(nameof(Organization), organizationId);

        var settings = organization.Settings
                       ?? throw new NotFoundException("OrganizationSettings", organizationId);

        var before = new
        {
            settings.DefaultTaxRatePercentage,
            settings.DefaultOverheadPercentage,
            settings.DefaultMarkupPercentage,
            settings.DefaultMarginPercentage
        };

        settings.UpdatePricingDefaults(
            request.DefaultTaxRatePercentage,
            CustomersController.ParseEnum<TaxBasis>(request.TaxBasis, nameof(request.TaxBasis)),
            request.DefaultOverheadPercentage,
            CustomersController.ParseEnum<PricingStrategy>(
                request.DefaultPricingStrategy, nameof(request.DefaultPricingStrategy)),
            request.DefaultMarkupPercentage,
            request.DefaultMarginPercentage,
            request.DiscountBeforeTax);

        settings.UpdateDocumentDefaults(request.QuoteValidityDays, settings.LogoPath,
            request.QuoteTermsAndConditions, request.QuoteFooterNote);

        settings.UpdateNumberPrefixes(request.CustomerNumberPrefix, request.ProjectNumberPrefix,
            request.EstimateNumberPrefix, request.QuoteNumberPrefix, request.ChangeOrderNumberPrefix);

        await db.SaveChangesAsync(ct);

        await audit.RecordChangeAsync(organizationId, AuditAction.Update, nameof(OrganizationSettings),
            settings.Id.ToString(), before,
            new
            {
                settings.DefaultTaxRatePercentage,
                settings.DefaultOverheadPercentage,
                settings.DefaultMarkupPercentage,
                settings.DefaultMarginPercentage
            },
            "Pricing defaults updated.", ct);

        return Ok(ToResponse(organization));
    }

    // --- Members -----------------------------------------------------------------------------

    [HttpGet("members")]
    [HasPermission(Permissions.UsersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<MemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> ListMembers(CancellationToken ct)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var members = await db.OrganizationMembers
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new MemberResponse(
                m.Id, m.UserId, m.User!.Email, m.User.FirstName, m.User.LastName,
                m.Role!.Name, m.IsActive, m.User.EmailConfirmed, m.JoinedAt, m.User.LastLoginAt))
            .ToListAsync(ct);

        return Ok(members);
    }

    [HttpPut("members/{membershipId:guid}")]
    [HasPermission(Permissions.UsersManage)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberResponse>> UpdateMember(Guid membershipId,
        UpdateMemberRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var membership = await db.OrganizationMembers
                             .Include(m => m.User)
                             .Include(m => m.Role)
                             .FirstOrDefaultAsync(m => m.Id == membershipId &&
                                                       m.OrganizationId == organizationId, ct)
                         ?? throw new NotFoundException(nameof(OrganizationMember), membershipId);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName, ct)
                   ?? throw new ValidationFailedException(nameof(request.RoleName),
                       $"'{request.RoleName}' is not a valid role.");

        // An organization must keep at least one active Owner, or nobody can administer it.
        if (membership.Role?.Name == Roles.Owner && request.RoleName != Roles.Owner)
        {
            await EnsureAnotherOwnerExistsAsync(organizationId, membership.Id, ct);
        }

        var previousRole = membership.Role?.Name;
        membership.ChangeRole(role.Id);

        if (request.IsActive)
        {
            membership.Activate();
        }
        else
        {
            if (membership.Role?.Name == Roles.Owner || previousRole == Roles.Owner)
            {
                await EnsureAnotherOwnerExistsAsync(organizationId, membership.Id, ct);
            }
            membership.Deactivate();
        }

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.PermissionChange, nameof(OrganizationMember),
            membership.Id.ToString(),
            $"Role changed from {previousRole} to {request.RoleName}; active: {request.IsActive}.", ct);

        return Ok(new MemberResponse(membership.Id, membership.UserId, membership.User!.Email,
            membership.User.FirstName, membership.User.LastName, role.Name, membership.IsActive,
            membership.User.EmailConfirmed, membership.JoinedAt, membership.User.LastLoginAt));
    }

    // --- Invitations ---------------------------------------------------------------------------

    [HttpGet("invitations")]
    [HasPermission(Permissions.UsersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<InvitationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> ListInvitations(CancellationToken ct)
    {
        var organizationId = currentOrganization.RequireOrganizationId();
        var now = clock.UtcNow;

        var invitations = await db.UserInvitations
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationResponse(i.Id, i.Email, i.Role!.Name, i.ExpiresAt,
                i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now))
            .ToListAsync(ct);

        return Ok(invitations);
    }

    [HttpPost("invitations")]
    [HasPermission(Permissions.UsersInvite)]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvitationResponse>> Invite(InviteUserRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName, ct)
                   ?? throw new ValidationFailedException(nameof(request.RoleName),
                       $"'{request.RoleName}' is not a valid role.");

        var alreadyMember = await db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId &&
                           m.User!.NormalizedEmail == normalizedEmail, ct);

        if (alreadyMember)
        {
            throw new ConflictException("That person is already a member of this organization.");
        }

        var token = SecureToken.Generate();
        var invitation = UserInvitation.Create(organizationId, request.Email, role.Id,
            SecureToken.Hash(token), clock.UtcNow, authOptions.InvitationLifetime);

        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var organizationName = await db.Organizations
            .Where(o => o.Id == organizationId)
            .Select(o => o.Name)
            .FirstAsync(ct);

        var link = $"{authOptions.WebAppBaseUrl.TrimEnd('/')}/auth/accept-invitation" +
                   $"?token={Uri.EscapeDataString(token)}";

        await emailSender.SendAsync(new EmailMessage(
            request.Email,
            $"You have been invited to join {organizationName} on Tile Estimator",
            $"""
             <p>You have been invited to join <strong>{organizationName}</strong> as a {role.Name}.</p>
             <p><a href="{link}">Accept the invitation</a></p>
             <p>This invitation expires in {authOptions.InvitationLifetime.TotalDays:0} days.</p>
             """,
            $"Accept your invitation: {link}"), ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(UserInvitation),
            invitation.Id.ToString(), $"Invited {request.Email} as {role.Name}.", ct);

        return Ok(new InvitationResponse(invitation.Id, invitation.Email, role.Name,
            invitation.ExpiresAt, true));
    }

    [HttpDelete("invitations/{id:guid}")]
    [HasPermission(Permissions.UsersInvite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken ct)
    {
        var organizationId = currentOrganization.RequireOrganizationId();

        var invitation = await db.UserInvitations
                             .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, ct)
                         ?? throw new NotFoundException(nameof(UserInvitation), id);

        invitation.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // --- Audit log (SPEC 18) ----------------------------------------------------------------------

    [HttpGet("/api/audit-logs")]
    [HasPermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> AuditLogs(
        [FromQuery] AuditLogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            source = source.Where(a => a.EntityName == query.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            source = source.Where(a => a.EntityId == query.EntityId);
        }

        if (Enum.TryParse<AuditAction>(query.Action, true, out var action))
        {
            source = source.Where(a => a.Action == action);
        }

        if (query.UserId.HasValue)
        {
            source = source.Where(a => a.UserId == query.UserId.Value);
        }

        if (query.From.HasValue)
        {
            source = source.Where(a => a.CreatedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            source = source.Where(a => a.CreatedAt <= query.To.Value);
        }

        var total = await source.CountAsync(ct);

        var items = await source
            .OrderByDescending(a => a.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(a => new AuditLogResponse(a.Id, a.UserId, a.UserEmail, a.EntityName, a.EntityId,
                a.Action.ToString(), a.OldValues, a.NewValues, a.Summary, a.IpAddress,
                a.CorrelationId, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new PagedResult<AuditLogResponse>(items, query.Page, query.PageSize, total));
    }

    private async Task EnsureAnotherOwnerExistsAsync(Guid organizationId, Guid excludingMembershipId,
        CancellationToken ct)
    {
        var otherOwners = await db.OrganizationMembers
            .CountAsync(m => m.OrganizationId == organizationId &&
                             m.Id != excludingMembershipId &&
                             m.IsActive &&
                             m.Role!.Name == Roles.Owner, ct);

        if (otherOwners == 0)
        {
            throw new ConflictException(
                "An organization must keep at least one active Owner. Promote someone else first.");
        }
    }

    private static OrganizationSettingsResponse ToResponse(Organization organization)
    {
        var s = organization.Settings!;

        return new OrganizationSettingsResponse(
            organization.Id, organization.Name, organization.LegalName, organization.Email,
            organization.Phone, organization.Website, organization.LicenseNumber,
            organization.Address.ToResponse(),
            s.Currency, s.DefaultTaxRatePercentage, s.TaxBasis.ToString(),
            s.DefaultOverheadPercentage, s.DefaultPricingStrategy.ToString(),
            s.DefaultMarkupPercentage, s.DefaultMarginPercentage, s.DiscountBeforeTax,
            s.QuoteValidityDays, s.CustomerNumberPrefix, s.ProjectNumberPrefix,
            s.EstimateNumberPrefix, s.QuoteNumberPrefix, s.LogoPath,
            s.QuoteTermsAndConditions, s.QuoteFooterNote);
    }
}
