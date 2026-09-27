using System.ComponentModel.DataAnnotations;

namespace TileEstimator.Contracts.Auth;

/// <summary>SPEC 5 registration form.</summary>
public sealed record RegisterRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyName { get; init; } = string.Empty;

    public bool AcceptTerms { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    /// <summary>Optional when a user belongs to several organizations. Membership is still verified.</summary>
    public Guid? OrganizationId { get; init; }
}

public sealed record RefreshRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;

    public Guid? OrganizationId { get; init; }
}

/// <summary>
/// What a successful sign-in returns. The refresh token is returned once here and only its
/// hash is stored server-side.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool EmailConfirmed,
    Guid? ActiveOrganizationId,
    string? ActiveOrganizationName,
    string? RoleName,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<OrganizationMembershipResponse> Organizations);

public sealed record OrganizationMembershipResponse(Guid OrganizationId, string OrganizationName, string RoleName);

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed record VerifyEmailRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed record LogoutRequest
{
    public string? RefreshToken { get; init; }
}

// --- Members and invitations ---------------------------------------------------------------

public sealed record InviteUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string RoleName { get; init; } = string.Empty;
}

public sealed record AcceptInvitationRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record MemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    bool IsActive,
    bool EmailConfirmed,
    DateTime JoinedAt,
    DateTime? LastLoginAt);

public sealed record InvitationResponse(
    Guid Id,
    string Email,
    string RoleName,
    DateTime ExpiresAt,
    bool IsPending);

public sealed record UpdateMemberRequest
{
    [Required]
    public string RoleName { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}
