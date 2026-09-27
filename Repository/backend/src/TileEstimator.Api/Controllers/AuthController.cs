using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services.Auth;
using TileEstimator.Contracts.Auth;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// SPEC 5 authentication endpoints. Thin by design: every rule lives in
/// <see cref="AuthService"/>. Rate limited, because this is the most attacked surface.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
[Produces("application/json")]
public sealed class AuthController(
    AuthService authService,
    ICurrentUserService currentUser,
    ICurrentOrganizationService currentOrganization)
    : ControllerBase
{
    /// <summary>Registers a user and their organization in a single transaction.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        Ok(await authService.RegisterAsync(request, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));

    /// <summary>Exchanges a refresh token for a new pair. The presented token is rotated out.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await authService.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request?.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>The signed-in user's profile, active organization and permissions.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");
        return Ok(await authService.GetCurrentUserAsync(userId, currentOrganization.OrganizationId, ct));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await authService.VerifyEmailAsync(request, ct);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(ForgotPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authService.ResendVerificationAsync(request.Email, ct);
        return Accepted();
    }

    /// <summary>
    /// Starts a password reset. Always returns 202, whether or not the address is registered,
    /// so this cannot be used to find out who has an account.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
