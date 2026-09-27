using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.Quotes;
using TileEstimator.Domain.SystemModule;

namespace TileEstimator.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuoteNumber).HasMaxLength(32).IsRequired();
        builder.Property(q => q.Title).HasMaxLength(200);
        builder.Property(q => q.ScopeOfWork).HasMaxLength(8000);
        builder.Property(q => q.TermsAndConditions).HasMaxLength(8000);
        builder.Property(q => q.FooterNote).HasMaxLength(1000);
        builder.Property(q => q.Currency).HasMaxLength(3).IsRequired();

        foreach (var money in new[]
                 {
                     nameof(Quote.MaterialTotal), nameof(Quote.LaborTotal), nameof(Quote.OtherTotal),
                     nameof(Quote.Subtotal), nameof(Quote.DiscountAmount), nameof(Quote.TaxAmount),
                     nameof(Quote.GrandTotal)
                 })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        // Snapshot of both parties as they were when the quote was produced.
        builder.Property(q => q.CustomerDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(q => q.CustomerEmail).HasMaxLength(256);
        builder.Property(q => q.CustomerPhone).HasMaxLength(40);
        builder.Property(q => q.CustomerAddressLine).HasMaxLength(500);
        builder.Property(q => q.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(q => q.CompanyEmail).HasMaxLength(256);
        builder.Property(q => q.CompanyPhone).HasMaxLength(40);
        builder.Property(q => q.CompanyAddressLine).HasMaxLength(500);
        builder.Property(q => q.CompanyLicenseNumber).HasMaxLength(100);
        builder.Property(q => q.CompanyLogoPath).HasMaxLength(512);

        builder.Property(q => q.PublicTokenHash).HasMaxLength(128);
        builder.Property(q => q.PdfStorageKey).HasMaxLength(512);

        builder.Property(q => q.RowVersion).IsRowVersion();

        builder.HasOne(q => q.Project).WithMany()
            .HasForeignKey(q => q.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(q => q.Estimate).WithMany()
            .HasForeignKey(q => q.EstimateId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(q => q.Customer).WithMany()
            .HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(q => new { q.OrganizationId, q.QuoteNumber, q.Version })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(q => new { q.OrganizationId, q.Status });
        builder.HasIndex(q => new { q.OrganizationId, q.ProjectId });

        // The public quote page looks a quote up by token hash alone, with no tenant context,
        // so this index is deliberately not led by OrganizationId.
        builder.HasIndex(q => q.PublicTokenHash).IsUnique().HasFilter("[PublicTokenHash] IS NOT NULL");
    }
}

public sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("QuoteLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.RoomName).HasMaxLength(150);
        builder.Property(l => l.Unit).HasMaxLength(16).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.TotalPrice).HasPrecision(18, 2);

        builder.HasOne(l => l.Quote).WithMany(q => q.Lines)
            .HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EstimateLine>().WithMany()
            .HasForeignKey(l => l.EstimateLineId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(l => new { l.OrganizationId, l.QuoteId });
    }
}

public sealed class QuoteRecipientConfiguration : IEntityTypeConfiguration<QuoteRecipient>
{
    public void Configure(EntityTypeBuilder<QuoteRecipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("QuoteRecipients");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Email).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(150);
        builder.Property(r => r.DeliveryError).HasMaxLength(1000);

        builder.HasOne(r => r.Quote).WithMany(q => q.Recipients)
            .HasForeignKey(r => r.QuoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.OrganizationId, r.QuoteId });
    }
}

public sealed class QuoteApprovalConfiguration : IEntityTypeConfiguration<QuoteApproval>
{
    public void Configure(EntityTypeBuilder<QuoteApproval> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("QuoteApprovals");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CustomerEmail).HasMaxLength(256);
        builder.Property(a => a.Comments).HasMaxLength(4000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);

        builder.HasOne(a => a.Quote).WithMany(q => q.Approvals)
            .HasForeignKey(a => a.QuoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.OrganizationId, a.QuoteId });
    }
}

public sealed class ChangeOrderConfiguration : IEntityTypeConfiguration<ChangeOrder>
{
    public void Configure(EntityTypeBuilder<ChangeOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ChangeOrders");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(2000);
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasOne<Project>().WithMany()
            .HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Quote>().WithMany()
            .HasForeignKey(c => c.QuoteId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(c => new { c.OrganizationId, c.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.OrganizationId, c.ProjectId });
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.UserEmail).HasMaxLength(256);
        builder.Property(a => a.OldValues).HasMaxLength(8000);
        builder.Property(a => a.NewValues).HasMaxLength(8000);
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);

        // No FK to Organizations: audit rows outlive the rows they describe and must never be
        // removed by a cascade. Audit is append-only and is not soft-deletable.
        builder.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
        builder.HasIndex(a => new { a.OrganizationId, a.EntityName, a.EntityId });
        builder.HasIndex(a => new { a.OrganizationId, a.UserId });
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000);
        builder.Property(n => n.Link).HasMaxLength(512);

        builder.HasIndex(n => new { n.OrganizationId, n.UserId, n.ReadAt });
    }
}

public sealed class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ApplicationSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(2000);
        builder.Property(s => s.Description).HasMaxLength(500);

        builder.HasIndex(s => s.Key).IsUnique();
    }
}
