using TileEstimator.Domain.Common;
using TileEstimator.Domain.Identity;

namespace TileEstimator.Domain.Organizations;

/// <summary>
/// The tenant root. Every tenant-owned entity points at exactly one organization (SPEC 4).
/// </summary>
public class Organization : AuditableEntity, ISoftDeletable
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Website { get; private set; }
    public string? LicenseNumber { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Address? Address { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public OrganizationSettings? Settings { get; private set; }
    public ICollection<OrganizationMember> Members { get; private set; } = new List<OrganizationMember>();

    private Organization() { }

    public static Organization Create(string name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Organization name is required.");
        return new Organization
        {
            Name = name.Trim(),
            Slug = Slugify(name)
        };
    }

    public void UpdateProfile(string name, string? legalName, string? email, string? phone, string? website, string? licenseNumber, Address? address)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Organization name is required.");
        Name = name.Trim();
        LegalName = legalName?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        Website = website?.Trim();
        LicenseNumber = licenseNumber?.Trim();
        Address = address;
    }

    public void AttachSettings(OrganizationSettings settings) => Settings = settings;

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        return slug.Trim('-');
    }
}

/// <summary>
/// Links a user to an organization with a role. Membership is the only proof of tenancy:
/// the server validates it on every request and never trusts an OrganizationId from the client.
/// </summary>
public class OrganizationMember : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime JoinedAt { get; private set; }

    public Organization? Organization { get; private set; }
    public User? User { get; private set; }
    public Role? Role { get; private set; }

    private OrganizationMember() { }

    public static OrganizationMember Create(Guid organizationId, Guid userId, Guid roleId, DateTime utcNow) => new()
    {
        OrganizationId = organizationId,
        UserId = userId,
        RoleId = roleId,
        JoinedAt = utcNow
    };

    public void ChangeRole(Guid roleId)
    {
        DomainException.Require(roleId != Guid.Empty, "Role is required.");
        RoleId = roleId;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
