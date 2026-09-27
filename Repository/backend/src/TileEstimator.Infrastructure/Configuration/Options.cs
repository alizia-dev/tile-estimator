using System.ComponentModel.DataAnnotations;

namespace TileEstimator.Infrastructure.Configuration;

/// <summary>JWT settings (SPEC 19). The secret comes from user-secrets or the environment, never the repo.</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32, ErrorMessage = "The JWT secret must be at least 32 characters.")]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "TileEstimator";

    [Required]
    public string Audience { get; set; } = "TileEstimator";

    /// <summary>Access tokens are short-lived; the refresh token carries the session.</summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Applies pending migrations at startup. Convenient locally, off in production.</summary>
    public bool AutoMigrate { get; set; }

    /// <summary>Seeds the demo organization. Development only (SPEC 22).</summary>
    public bool SeedDemoData { get; set; }

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Root folder for the local disk provider. Blob Storage replaces this later.</summary>
    public string RootPath { get; set; } = "storage";

    [Range(1, 100)]
    public int MaxUploadMegabytes { get; set; } = 25;

    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".dwg", ".docx", ".xlsx", ".csv", ".txt"];
}

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    [Required]
    public string FromAddress { get; set; } = "no-reply@tileestimator.local";

    [Required]
    public string FromName { get; set; } = "Tile Estimator";

    /// <summary>
    /// The development sender writes each message to disk instead of sending it, so the whole
    /// verification and quote flow can be exercised without an SMTP account.
    /// </summary>
    public string OutboxPath { get; set; } = "outbox";
}

public sealed class ApplicationSettings
{
    public const string SectionName = "Application";

    /// <summary>Base URL of the Angular app, used to build verification and quote links.</summary>
    [Required]
    public string WebAppBaseUrl { get; set; } = "http://localhost:4200";

    public string[] CorsOrigins { get; set; } = ["http://localhost:4200"];

    [Range(1, 168)]
    public int EmailVerificationHours { get; set; } = 48;

    [Range(1, 24)]
    public int PasswordResetHours { get; set; } = 2;

    [Range(1, 90)]
    public int InvitationDays { get; set; } = 14;

    /// <summary>How long a public quote link stays usable beyond the quote's own expiry.</summary>
    [Range(1, 180)]
    public int QuoteLinkGraceDays { get; set; } = 7;

    [Range(1, 20)]
    public int MaxFailedLoginAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}
