using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TileEstimator.Application.Abstractions;
using TileEstimator.Infrastructure.Configuration;

namespace TileEstimator.Infrastructure.Services;

/// <summary>Wraps the ASP.NET Core Identity password hasher (PBKDF2) behind our own interface.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private sealed class HashTarget;

    private readonly PasswordHasher<HashTarget> _hasher = new();
    private static readonly HashTarget Target = new();

    public string Hash(string password) => _hasher.HashPassword(Target, password);

    public (bool Succeeded, bool NeedsRehash) Verify(string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(Target, hash, password);
        return result switch
        {
            PasswordVerificationResult.Success => (true, false),
            PasswordVerificationResult.SuccessRehashNeeded => (true, true),
            _ => (false, false)
        };
    }
}

/// <summary>
/// Issues short-lived access tokens. The JWT carries identity and the active organization only:
/// no prices, no customer data, nothing that would go stale or leak if the token were captured.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtSettings> settings, IDateTimeProvider clock) : IJwtTokenService
{
    /// <summary>Claim holding the active organization. Still re-verified server-side on every request.</summary>
    public const string OrganizationClaim = "org";

    /// <summary>Permission claim type, used by the permission policies.</summary>
    public const string PermissionClaim = "perm";

    private readonly JwtSettings _settings = settings.Value;

    public string CreateAccessToken(Guid userId, string email, Guid? organizationId, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var now = clock.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (organizationId.HasValue)
        {
            claims.Add(new Claim(OrganizationClaim, organizationId.Value.ToString()));
        }

        claims.AddRange(permissions.Select(p => new Claim(PermissionClaim, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_settings.AccessTokenMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetAccessTokenExpiry() => clock.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
}
