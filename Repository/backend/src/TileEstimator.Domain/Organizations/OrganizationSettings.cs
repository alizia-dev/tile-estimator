using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Identity;

namespace TileEstimator.Domain.Organizations;

/// <summary>
/// Everything an organization configures instead of us hard-coding it (SPEC 7 of CLAUDE.md):
/// currency, tax, overhead, default pricing strategy and document numbering.
/// Values here are defaults for new estimates only; each estimate stores its own snapshot.
/// </summary>
public class OrganizationSettings : AuditableEntity
{
    public Guid OrganizationId { get; private set; }

    public string Currency { get; private set; } = "USD";

    /// <summary>Default sales tax rate as a percentage, e.g. 8.25 for 8.25%.</summary>
    public decimal DefaultTaxRatePercentage { get; private set; }

    /// <summary>What the tax applies to. US tax treatment of labor varies by state.</summary>
    public TaxBasis TaxBasis { get; private set; } = TaxBasis.MaterialsAndLabor;

    public decimal DefaultOverheadPercentage { get; private set; }

    public PricingStrategy DefaultPricingStrategy { get; private set; } = PricingStrategy.Markup;

    public decimal DefaultMarkupPercentage { get; private set; } = 20m;

    public decimal DefaultMarginPercentage { get; private set; } = 25m;

    /// <summary>True applies the discount to the pre-tax subtotal; false discounts the taxed total.</summary>
    public bool DiscountBeforeTax { get; private set; } = true;

    public int QuoteValidityDays { get; private set; } = 30;

    public string CustomerNumberPrefix { get; private set; } = "CUST";
    public string ProjectNumberPrefix { get; private set; } = "PRJ";
    public string EstimateNumberPrefix { get; private set; } = "EST";
    public string QuoteNumberPrefix { get; private set; } = "QTE";
    public string ChangeOrderNumberPrefix { get; private set; } = "CO";

    public string? LogoPath { get; private set; }
    public string? QuoteTermsAndConditions { get; private set; }
    public string? QuoteFooterNote { get; private set; }

    public Organization? Organization { get; private set; }

    private OrganizationSettings() { }

    public static OrganizationSettings CreateDefault(Guid organizationId) => new()
    {
        OrganizationId = organizationId
    };

    public void UpdatePricingDefaults(
        decimal defaultTaxRatePercentage,
        TaxBasis taxBasis,
        decimal defaultOverheadPercentage,
        PricingStrategy defaultPricingStrategy,
        decimal defaultMarkupPercentage,
        decimal defaultMarginPercentage,
        bool discountBeforeTax)
    {
        DomainException.Require(defaultTaxRatePercentage >= 0m, "Tax rate cannot be negative.");
        DomainException.Require(defaultOverheadPercentage >= 0m, "Overhead cannot be negative.");
        DomainException.Require(defaultMarkupPercentage >= 0m, "Markup cannot be negative.");
        DomainException.Require(defaultMarginPercentage >= 0m && defaultMarginPercentage < 100m,
            "Margin must be at least 0% and below 100%.");

        DefaultTaxRatePercentage = defaultTaxRatePercentage;
        TaxBasis = taxBasis;
        DefaultOverheadPercentage = defaultOverheadPercentage;
        DefaultPricingStrategy = defaultPricingStrategy;
        DefaultMarkupPercentage = defaultMarkupPercentage;
        DefaultMarginPercentage = defaultMarginPercentage;
        DiscountBeforeTax = discountBeforeTax;
    }

    public void UpdateDocumentDefaults(int quoteValidityDays, string? logoPath, string? terms, string? footerNote)
    {
        DomainException.Require(quoteValidityDays > 0, "Quote validity must be at least one day.");
        QuoteValidityDays = quoteValidityDays;
        LogoPath = logoPath;
        QuoteTermsAndConditions = terms;
        QuoteFooterNote = footerNote;
    }

    public void UpdateNumberPrefixes(string customer, string project, string estimate, string quote, string changeOrder)
    {
        CustomerNumberPrefix = Normalize(customer, "CUST");
        ProjectNumberPrefix = Normalize(project, "PRJ");
        EstimateNumberPrefix = Normalize(estimate, "EST");
        QuoteNumberPrefix = Normalize(quote, "QTE");
        ChangeOrderNumberPrefix = Normalize(changeOrder, "CO");
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
}

/// <summary>
/// Per-organization counter behind CUST-1001, PRJ-1001, EST-1001 and so on.
/// Incremented inside the same transaction as the row it numbers, so numbers never collide.
/// </summary>
public class NumberSequence : Entity
{
    public Guid OrganizationId { get; private set; }

    /// <summary>Sequence key: Customer, Project, Estimate, Quote, ChangeOrder.</summary>
    public string EntityType { get; private set; } = string.Empty;

    public int NextValue { get; private set; } = 1001;

    private NumberSequence() { }

    public static NumberSequence Create(Guid organizationId, string entityType, int startAt = 1001) => new()
    {
        OrganizationId = organizationId,
        EntityType = entityType,
        NextValue = startAt
    };

    /// <summary>Returns the current value and advances the counter.</summary>
    public int Take()
    {
        var value = NextValue;
        NextValue++;
        return value;
    }
}

/// <summary>An invitation for a person to join an organization with a given role (SPEC 6).</summary>
public class UserInvitation : AuditableEntity, ITenantOwned
{
    public Guid OrganizationId { get; set; }
    public string Email { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public Role? Role { get; private set; }

    private UserInvitation() { }

    public static UserInvitation Create(Guid organizationId, string email, Guid roleId, string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(email), "Email is required.");
        return new UserInvitation
        {
            OrganizationId = organizationId,
            Email = email.Trim().ToUpperInvariant(),
            RoleId = roleId,
            TokenHash = tokenHash,
            ExpiresAt = utcNow.Add(lifetime)
        };
    }

    public bool IsPending(DateTime utcNow) => AcceptedAt is null && RevokedAt is null && utcNow < ExpiresAt;

    public void Accept(Guid userId, DateTime utcNow)
    {
        DomainException.Require(IsPending(utcNow), "This invitation is no longer valid.");
        AcceptedAt = utcNow;
        AcceptedByUserId = userId;
    }

    public void Revoke(DateTime utcNow) => RevokedAt = utcNow;
}
