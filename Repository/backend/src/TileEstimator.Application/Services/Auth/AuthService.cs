using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Contracts.Auth;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.SystemModule;

namespace TileEstimator.Application.Services.Auth;

/// <summary>Knobs the API layer supplies from configuration, so this service stays testable.</summary>
public sealed record AuthOptions(
    TimeSpan RefreshTokenLifetime,
    TimeSpan EmailVerificationLifetime,
    TimeSpan PasswordResetLifetime,
    TimeSpan InvitationLifetime,
    int MaxFailedLoginAttempts,
    int LockoutMinutes,
    string WebAppBaseUrl);

/// <summary>
/// SPEC 5 and SPEC 6: registration, sign-in, refresh-token rotation, email verification,
/// password reset and organization membership.
/// </summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ICurrentOrganizationService currentOrganization,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    IEmailSender emailSender,
    IOrganizationProvisioningService provisioning,
    IAuditService audit,
    AuthOptions options,
    ILogger<AuthService> logger)
{
    /// <summary>
    /// SPEC 5 registration, as one transaction: user, organization, membership with the Owner
    /// role, default settings and the default estimation configuration. If any step fails,
    /// none of it is kept, so a half-provisioned tenant can never exist.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new ValidationFailedException(nameof(request.ConfirmPassword), "The passwords do not match.");
        }

        if (!request.AcceptTerms)
        {
            throw new ValidationFailedException(nameof(request.AcceptTerms), "You must accept the terms to continue.");
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        // There is no tenant yet, so tenant filtering is suspended for the whole transaction.
        using var bypass = currentOrganization.BypassTenantFilter();

        if (await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail && !u.IsDeleted, cancellationToken))
        {
            throw new ConflictException("An account already exists for that email address.");
        }

        var ownerRoleId = await db.Roles
            .Where(r => r.Name == Roles.Owner)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerRoleId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The Owner role is missing. Run the system seeder before accepting registrations.");
        }

        var now = clock.UtcNow;

        // Built outside the transaction block so a retry reuses the same ids rather than
        // generating a second set.
        var user = User.Create(request.Email, passwordHasher.Hash(request.Password),
            request.FirstName, request.LastName);
        var organization = Organization.Create(request.CompanyName);
        var verificationToken = SecureToken.Generate();

        await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Users.Add(user);
            db.Organizations.Add(organization);
            db.OrganizationMembers.Add(OrganizationMember.Create(organization.Id, user.Id, ownerRoleId, now));
            db.OrganizationSettings.Add(OrganizationSettings.CreateDefault(organization.Id));

            await db.SaveChangesAsync(ct);

            // Patterns, waste rules, materials, labor rates and assemblies for the new tenant.
            await provisioning.ProvisionDefaultsAsync(organization.Id, ct);

            db.UserTokens.Add(UserToken.Issue(user.Id, UserToken.EmailVerification,
                SecureToken.Hash(verificationToken), now, options.EmailVerificationLifetime));

            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        // Email is sent only after the transaction commits, so a rolled-back registration
        // never results in a verification link for an account that does not exist.
        await SendVerificationEmailAsync(user, verificationToken, cancellationToken);

        await audit.RecordAsync(organization.Id, AuditAction.Create, nameof(Organization),
            organization.Id.ToString(), $"Organization '{organization.Name}' registered.", cancellationToken);

        logger.LogInformation("Registered organization {OrganizationId} for user {UserId}.",
            organization.Id, user.Id);

        return await IssueTokensAsync(user, organization.Id, cancellationToken);
    }

    /// <summary>
    /// Sign-in. A wrong password, an unknown email and a deactivated account all produce the
    /// same message, so the endpoint cannot be used to discover which accounts exist.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        const string GenericFailure = "That email address and password do not match an account.";
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            // Still hash something, so a missing account is not faster than a wrong password.
            passwordHasher.Verify(
                "AQAAAAIAAYagAAAAEPlaceholderHashValueForTimingEqualisationOnly==", request.Password);
            throw new ForbiddenException(GenericFailure);
        }

        var now = clock.UtcNow;

        if (user.IsLockedOut(now))
        {
            throw new ForbiddenException(
                "This account is temporarily locked after too many failed sign-in attempts. Try again shortly.");
        }

        var (succeeded, needsRehash) = passwordHasher.Verify(user.PasswordHash, request.Password);

        if (!succeeded)
        {
            user.RecordFailedLogin(now, options.MaxFailedLoginAttempts, options.LockoutMinutes);
            await db.SaveChangesAsync(cancellationToken);

            await audit.RecordAsync(Guid.Empty, AuditAction.LoginFailed, nameof(User),
                user.Id.ToString(), "Failed sign-in attempt.", cancellationToken);

            throw new ForbiddenException(GenericFailure);
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("This account has been deactivated. Contact your organization owner.");
        }

        if (needsRehash)
        {
            user.SetPasswordHash(passwordHasher.Hash(request.Password));
        }

        user.RecordSuccessfulLogin(now);

        var organizationId = await ResolveOrganizationAsync(user.Id, request.OrganizationId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (organizationId.HasValue)
        {
            await audit.RecordAsync(organizationId.Value, AuditAction.Login, nameof(User),
                user.Id.ToString(), "Signed in.", cancellationToken);
        }

        return await IssueTokensAsync(user, organizationId, cancellationToken);
    }

    /// <summary>
    /// Rotates a refresh token. Presenting a token that was already rotated means the token was
    /// stolen or replayed, so the whole family is revoked rather than just refused.
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var bypass = currentOrganization.BypassTenantFilter();

        var tokenHash = SecureToken.Hash(request.RefreshToken);
        var now = clock.UtcNow;

        var stored = await db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            throw new ForbiddenException("That refresh token is not valid.");
        }

        if (stored.IsRevoked)
        {
            await RevokeAllTokensAsync(stored.UserId, "Reuse of a revoked refresh token detected.", cancellationToken);
            logger.LogWarning("Refresh-token reuse detected for user {UserId}; all sessions revoked.", stored.UserId);
            throw new ForbiddenException("That refresh token is not valid. Please sign in again.");
        }

        if (stored.IsExpired(now))
        {
            throw new ForbiddenException("Your session has expired. Please sign in again.");
        }

        var user = stored.User ?? throw new ForbiddenException("That refresh token is not valid.");

        if (!user.IsActive || user.IsDeleted)
        {
            throw new ForbiddenException("This account is no longer active.");
        }

        var organizationId = await ResolveOrganizationAsync(user.Id, request.OrganizationId, cancellationToken);
        var response = await IssueTokensAsync(user, organizationId, cancellationToken, stored);

        return response;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = SecureToken.Hash(refreshToken);
            var stored = await db.RefreshTokens.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

            stored?.Revoke(clock.UtcNow, currentUser.IpAddress, "Signed out.");
        }
        else if (currentUser.UserId is { } userId)
        {
            await RevokeAllTokensAsync(userId, "Signed out.", cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (currentOrganization.OrganizationId is { } orgId && currentUser.UserId is not null)
        {
            await audit.RecordAsync(orgId, AuditAction.Logout, nameof(User),
                currentUser.UserId.ToString(), "Signed out.", cancellationToken);
        }
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await FindUserByEmailAsync(request.Email, cancellationToken)
                   ?? throw new ValidationFailedException(nameof(request.Token),
                       "That verification link is not valid or has expired.");

        var token = await ConsumeUserTokenAsync(user.Id, UserToken.EmailVerification, request.Token, cancellationToken);

        if (token is null)
        {
            throw new ValidationFailedException(nameof(request.Token),
                "That verification link is not valid or has expired.");
        }

        user.ConfirmEmail();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken cancellationToken)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await FindUserByEmailAsync(email, cancellationToken);

        // Silently succeed for unknown or already-verified addresses so this cannot enumerate users.
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        var (token, _) = await IssueUserTokenAsync(
            user.Id, UserToken.EmailVerification, options.EmailVerificationLifetime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await SendVerificationEmailAsync(user, token, cancellationToken);
    }

    /// <summary>Always reports success, so the endpoint cannot be used to find registered addresses.</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var (token, _) = await IssueUserTokenAsync(
            user.Id, UserToken.PasswordReset, options.PasswordResetLifetime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var link = $"{options.WebAppBaseUrl.TrimEnd('/')}/auth/reset-password" +
                   $"?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendAsync(new EmailMessage(
            user.Email,
            "Reset your Tile Estimator password",
            $"""
             <p>Hello {user.FirstName},</p>
             <p>Use the link below to choose a new password. It expires in
                {options.PasswordResetLifetime.TotalHours:0} hours.</p>
             <p><a href="{link}">Reset your password</a></p>
             <p>If you did not ask for this, you can ignore this message; your password will not change.</p>
             """,
            $"Reset your password: {link}"), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new ValidationFailedException(nameof(request.ConfirmPassword), "The passwords do not match.");
        }

        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await FindUserByEmailAsync(request.Email, cancellationToken)
                   ?? throw new ValidationFailedException(nameof(request.Token),
                       "That reset link is not valid or has expired.");

        var token = await ConsumeUserTokenAsync(user.Id, UserToken.PasswordReset, request.Token, cancellationToken)
                    ?? throw new ValidationFailedException(nameof(request.Token),
                        "That reset link is not valid or has expired.");

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));

        // A password reset ends every existing session.
        await RevokeAllTokensAsync(user.Id, "Password was reset.", cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new ValidationFailedException(nameof(request.ConfirmPassword), "The passwords do not match.");
        }

        var userId = currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");

        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await db.Users.IgnoreQueryFilters()
                       .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new NotFoundException(nameof(User), userId);

        var (succeeded, _) = passwordHasher.Verify(user.PasswordHash, request.CurrentPassword);
        if (!succeeded)
        {
            throw new ValidationFailedException(nameof(request.CurrentPassword),
                "That is not your current password.");
        }

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        await RevokeAllTokensAsync(user.Id, "Password was changed.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Builds the profile the client needs: identity, active organization and permissions.</summary>
    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, Guid? organizationId,
        CancellationToken cancellationToken)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        var user = await db.Users.IgnoreQueryFilters()
                       .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken)
                   ?? throw new NotFoundException(nameof(User), userId);

        var memberships = await db.OrganizationMembers.IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => new
            {
                m.OrganizationId,
                OrganizationName = m.Organization!.Name,
                RoleName = m.Role!.Name
            })
            .ToListAsync(cancellationToken);

        var active = organizationId.HasValue
            ? memberships.FirstOrDefault(m => m.OrganizationId == organizationId.Value)
            : memberships.FirstOrDefault();

        var permissions = active is null
            ? []
            : await GetPermissionsAsync(active.RoleName, cancellationToken);

        return new CurrentUserResponse(
            user.Id, user.Email, user.FirstName, user.LastName, user.EmailConfirmed,
            active?.OrganizationId, active?.OrganizationName, active?.RoleName,
            permissions,
            memberships
                .Select(m => new OrganizationMembershipResponse(m.OrganizationId, m.OrganizationName, m.RoleName))
                .ToList());
    }

    /// <summary>
    /// Confirms the user really belongs to the organization they asked for. This is the check
    /// that makes an OrganizationId from the client harmless.
    /// </summary>
    public async Task<(Guid OrganizationId, string RoleName, IReadOnlyList<string> Permissions)?>
        ResolveMembershipAsync(Guid userId, Guid? requestedOrganizationId, CancellationToken cancellationToken)
    {
        using var bypass = currentOrganization.BypassTenantFilter();

        var query = db.OrganizationMembers.IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.IsActive);

        if (requestedOrganizationId.HasValue)
        {
            query = query.Where(m => m.OrganizationId == requestedOrganizationId.Value);
        }

        var membership = await query
            .OrderBy(m => m.JoinedAt)
            .Select(m => new { m.OrganizationId, RoleName = m.Role!.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var permissions = await GetPermissionsAsync(membership.RoleName, cancellationToken);
        return (membership.OrganizationId, membership.RoleName, permissions);
    }

    private async Task<IReadOnlyList<string>> GetPermissionsAsync(string roleName, CancellationToken cancellationToken)
    {
        var codes = await db.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.Role!.Name == roleName)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync(cancellationToken);

        // Fall back to the compiled matrix if the seed has not caught up with a new permission.
        return codes.Count > 0
            ? codes
            : Roles.Matrix.TryGetValue(roleName, out var fallback) ? fallback : [];
    }

    private async Task<Guid?> ResolveOrganizationAsync(Guid userId, Guid? requested,
        CancellationToken cancellationToken)
    {
        var membership = await ResolveMembershipAsync(userId, requested, cancellationToken);

        if (membership is null && requested.HasValue)
        {
            throw new ForbiddenException("You are not a member of that organization.");
        }

        return membership?.OrganizationId;
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Guid? organizationId,
        CancellationToken cancellationToken, RefreshToken? rotating = null)
    {
        var now = clock.UtcNow;

        var permissions = organizationId.HasValue
            ? (await ResolveMembershipAsync(user.Id, organizationId, cancellationToken))?.Permissions ?? []
            : [];

        var accessToken = jwtTokenService.CreateAccessToken(user.Id, user.Email, organizationId, permissions);

        var refreshTokenValue = SecureToken.Generate();
        var refreshTokenHash = SecureToken.Hash(refreshTokenValue);

        var refreshToken = RefreshToken.Issue(user.Id, refreshTokenHash, now,
            options.RefreshTokenLifetime, currentUser.IpAddress);
        db.RefreshTokens.Add(refreshToken);

        // Rotation: the presented token is revoked and points at its successor.
        rotating?.Revoke(now, currentUser.IpAddress, "Rotated on refresh.", refreshTokenHash);

        await db.SaveChangesAsync(cancellationToken);

        var profile = await GetCurrentUserAsync(user.Id, organizationId, cancellationToken);

        return new AuthResponse(
            accessToken,
            jwtTokenService.GetAccessTokenExpiry(),
            refreshTokenValue,
            refreshToken.ExpiresAt,
            profile);
    }

    private async Task RevokeAllTokensAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(now, currentUser.IpAddress, reason);
        }
    }

    private async Task<(string Token, UserToken Entity)> IssueUserTokenAsync(Guid userId, string purpose,
        TimeSpan lifetime, CancellationToken cancellationToken)
    {
        // Only one token of each purpose is live at a time; issuing a new one retires the old.
        var existing = await db.UserTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        foreach (var token in existing)
        {
            token.Consume(now);
        }

        var value = SecureToken.Generate();
        var entity = UserToken.Issue(userId, purpose, SecureToken.Hash(value), now, lifetime);
        db.UserTokens.Add(entity);

        return (value, entity);
    }

    private async Task<UserToken?> ConsumeUserTokenAsync(Guid userId, string purpose, string token,
        CancellationToken cancellationToken)
    {
        var hash = SecureToken.Hash(token);
        var now = clock.UtcNow;

        var stored = await db.UserTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Purpose == purpose && t.TokenHash == hash,
                cancellationToken);

        if (stored is null || !stored.IsUsable(now))
        {
            return null;
        }

        stored.Consume(now);
        return stored;
    }

    private Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized && !u.IsDeleted, cancellationToken);
    }

    private Task SendVerificationEmailAsync(User user, string token, CancellationToken cancellationToken)
    {
        var link = $"{options.WebAppBaseUrl.TrimEnd('/')}/auth/verify-email" +
                   $"?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

        return emailSender.SendAsync(new EmailMessage(
            user.Email,
            "Confirm your Tile Estimator email address",
            $"""
             <p>Welcome, {user.FirstName}.</p>
             <p>Confirm your email address to finish setting up your account.</p>
             <p><a href="{link}">Confirm my email address</a></p>
             <p>This link expires in {options.EmailVerificationLifetime.TotalHours:0} hours.</p>
             """,
            $"Confirm your email address: {link}"), cancellationToken);
    }
}
