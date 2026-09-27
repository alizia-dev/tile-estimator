using TileEstimator.Domain.Common;
using TileEstimator.Domain.Organizations;

namespace TileEstimator.Domain.Identity;

/// <summary>
/// A person who can sign in. A user is NOT tenant-owned: membership in an organization
/// is expressed by <see cref="OrganizationMember"/> so one user can belong to several orgs (SPEC 4).
/// </summary>
public class User : AuditableEntity, ISoftDeletable
{
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndsAt { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<OrganizationMember> Memberships { get; private set; } = new List<OrganizationMember>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { }

    public static User Create(string email, string passwordHash, string firstName, string lastName)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(email), "Email is required.");
        DomainException.Require(email.Contains('@'), "Email is not valid.");
        DomainException.Require(!string.IsNullOrWhiteSpace(passwordHash), "Password hash is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(firstName), "First name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(lastName), "Last name is required.");

        return new User
        {
            Email = email.Trim(),
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim()
        };
    }

    public string FullName => FirstName + " " + LastName;

    public void ConfirmEmail() => EmailConfirmed = true;

    public void SetPasswordHash(string hash)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(hash), "Password hash is required.");
        PasswordHash = hash;
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
    }

    public void UpdateProfile(string firstName, string lastName, string? phoneNumber)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(firstName), "First name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(lastName), "Last name is required.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void RecordSuccessfulLogin(DateTime utcNow)
    {
        LastLoginAt = utcNow;
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
    }

    /// <summary>Locks the account for a period once the failure threshold is reached.</summary>
    public void RecordFailedLogin(DateTime utcNow, int maxAttempts, int lockoutMinutes)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndsAt = utcNow.AddMinutes(lockoutMinutes);
            FailedLoginAttempts = 0;
        }
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndsAt.HasValue && LockoutEndsAt.Value > utcNow;
}
