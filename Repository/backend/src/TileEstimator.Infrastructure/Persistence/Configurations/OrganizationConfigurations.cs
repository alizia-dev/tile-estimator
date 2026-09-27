using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;

namespace TileEstimator.Infrastructure.Persistence.Configurations;

/// <summary>Shared shape for the owned Address value object so every address column matches.</summary>
internal static class AddressConfiguration
{
    public static void ConfigureAddress<TOwner>(
        OwnedNavigationBuilder<TOwner, Address> builder, string prefix)
        where TOwner : class
    {
        builder.Property(a => a.Line1).HasColumnName(prefix + "Line1").HasMaxLength(200);
        builder.Property(a => a.Line2).HasColumnName(prefix + "Line2").HasMaxLength(200);
        builder.Property(a => a.City).HasColumnName(prefix + "City").HasMaxLength(100);
        builder.Property(a => a.State).HasColumnName(prefix + "State").HasMaxLength(2);
        builder.Property(a => a.PostalCode).HasColumnName(prefix + "PostalCode").HasMaxLength(20);
        builder.Property(a => a.Country).HasColumnName(prefix + "Country").HasMaxLength(2);
    }
}

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(200).IsRequired();
        builder.Property(o => o.LegalName).HasMaxLength(200);
        builder.Property(o => o.Email).HasMaxLength(256);
        builder.Property(o => o.Phone).HasMaxLength(40);
        builder.Property(o => o.Website).HasMaxLength(256);
        builder.Property(o => o.LicenseNumber).HasMaxLength(100);

        builder.OwnsOne(o => o.Address, a => AddressConfiguration.ConfigureAddress(a, "Address"));

        builder.HasOne(o => o.Settings).WithOne(s => s.Organization)
            .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.Slug);
    }
}

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OrganizationMembers");
        builder.HasKey(m => m.Id);

        builder.HasOne(m => m.Organization).WithMany(o => o.Members)
            .HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.User).WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Role).WithMany()
            .HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);

        // A person holds exactly one role per organization.
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
    }
}

public sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OrganizationSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();

        // Percentages and rates keep 4 decimals; money keeps 2 (docs/estimation-engine.md).
        builder.Property(s => s.DefaultTaxRatePercentage).HasPrecision(18, 4);
        builder.Property(s => s.DefaultOverheadPercentage).HasPrecision(18, 4);
        builder.Property(s => s.DefaultMarkupPercentage).HasPrecision(18, 4);
        builder.Property(s => s.DefaultMarginPercentage).HasPrecision(18, 4);

        builder.Property(s => s.CustomerNumberPrefix).HasMaxLength(10).IsRequired();
        builder.Property(s => s.ProjectNumberPrefix).HasMaxLength(10).IsRequired();
        builder.Property(s => s.EstimateNumberPrefix).HasMaxLength(10).IsRequired();
        builder.Property(s => s.QuoteNumberPrefix).HasMaxLength(10).IsRequired();
        builder.Property(s => s.ChangeOrderNumberPrefix).HasMaxLength(10).IsRequired();
        builder.Property(s => s.LogoPath).HasMaxLength(512);
        builder.Property(s => s.QuoteTermsAndConditions).HasMaxLength(4000);
        builder.Property(s => s.QuoteFooterNote).HasMaxLength(1000);

        builder.HasIndex(s => s.OrganizationId).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrganizationSettings_Margin", "[DefaultMarginPercentage] >= 0 AND [DefaultMarginPercentage] < 100");
            t.HasCheckConstraint("CK_OrganizationSettings_Tax", "[DefaultTaxRatePercentage] >= 0");
            t.HasCheckConstraint("CK_OrganizationSettings_Validity", "[QuoteValidityDays] > 0");
        });
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("NumberSequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.EntityType).HasMaxLength(50).IsRequired();

        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);

        // One counter per organization per document type. The unique index is what stops two
        // concurrent requests from creating rival sequences for the same key.
        builder.HasIndex(s => new { s.OrganizationId, s.EntityType }).IsUnique();
    }
}
