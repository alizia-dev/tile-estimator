using System.Security.Cryptography;
using System.Text;
using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.Identity;

/// <summary>
/// A rotating refresh token. Only the hash is stored (SPEC 5). Rotation revokes the old token and
/// records its successor, so replaying a revoked token is detectable.
/// </summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? RevokedReason { get; private set; }

    public User? User { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime utcNow, TimeSpan lifetime, string? ip) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = utcNow,
        ExpiresAt = utcNow.Add(lifetime),
        CreatedByIp = ip
    };

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string? ip, string reason, string? replacedByTokenHash = null)
    {
        RevokedAt = utcNow;
        RevokedByIp = ip;
        RevokedReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}

/// <summary>One-time token for email verification and password reset. Stored hashed, never in the clear.</summary>
public class UserToken : Entity
{
    public const string EmailVerification = "email-verification";
    public const string PasswordReset = "password-reset";

    public Guid UserId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    private UserToken() { }

    public static UserToken Issue(Guid userId, string purpose, string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(purpose), "Token purpose is required.");
        return new UserToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(lifetime)
        };
    }

    public bool IsUsable(DateTime utcNow) => ConsumedAt is null && utcNow < ExpiresAt;

    public void Consume(DateTime utcNow) => ConsumedAt = utcNow;
}

/// <summary>Cryptographically strong, URL-safe opaque tokens for refresh, verification and quote links.</summary>
public static class SecureToken
{
    public static string Generate(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
