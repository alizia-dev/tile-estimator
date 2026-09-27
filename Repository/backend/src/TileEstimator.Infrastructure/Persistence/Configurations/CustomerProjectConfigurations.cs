using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.FirstName).HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.CompanyName).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(40);
        builder.Property(c => c.Notes).HasMaxLength(4000);

        builder.OwnsOne(c => c.BillingAddress, a => AddressConfiguration.ConfigureAddress(a, "Billing"));
        builder.OwnsOne(c => c.ServiceAddress, a => AddressConfiguration.ConfigureAddress(a, "Service"));

        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);

        // Tenant indexes always lead with OrganizationId (SPEC 22).
        builder.HasIndex(c => new { c.OrganizationId, c.CustomerNumber }).IsUnique();
        builder.HasIndex(c => new { c.OrganizationId, c.LastName });
        builder.HasIndex(c => new { c.OrganizationId, c.CompanyName });
        builder.HasIndex(c => new { c.OrganizationId, c.Status });
    }
}

public sealed class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomerContacts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(40);

        builder.HasOne(c => c.Customer).WithMany(c => c.Contacts)
            .HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.OrganizationId, c.CustomerId });
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProjectNumber).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000);

        builder.OwnsOne(p => p.SiteAddress, a => AddressConfiguration.ConfigureAddress(a, "Site"));

        builder.HasOne(p => p.Customer).WithMany()
            .HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.OrganizationId, p.ProjectNumber }).IsUnique();
        builder.HasIndex(p => new { p.OrganizationId, p.Status });
        builder.HasIndex(p => new { p.OrganizationId, p.CustomerId });
    }
}

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Rooms");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(150).IsRequired();
        builder.Property(r => r.CustomTypeName).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(2000);

        builder.HasOne(r => r.Project).WithMany(p => p.Rooms)
            .HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.OrganizationId, r.ProjectId });
    }
}

public sealed class SurfaceConfiguration : IEntityTypeConfiguration<Surface>
{
    public void Configure(EntityTypeBuilder<Surface> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Surfaces");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(2000);

        // Dimensions feed money, so they carry 4 decimals rather than being stored as floats.
        builder.Property(s => s.LengthFeet).HasPrecision(18, 4);
        builder.Property(s => s.WidthFeet).HasPrecision(18, 4);
        builder.Property(s => s.HeightFeet).HasPrecision(18, 4);
        builder.Property(s => s.AreaOverrideSquareFeet).HasPrecision(18, 4);
        builder.Property(s => s.TrimLinearFeet).HasPrecision(18, 4);
        builder.Property(s => s.WasteOverridePercentage).HasPrecision(18, 4);

        builder.HasOne(s => s.Room).WithMany(r => r.Surfaces)
            .HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.OrganizationId, s.RoomId });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Surfaces_Length", "[LengthFeet] > 0");
            t.HasCheckConstraint("CK_Surfaces_Waste", "[WasteOverridePercentage] IS NULL OR [WasteOverridePercentage] >= 0");
        });
    }
}

public sealed class OpeningConfiguration : IEntityTypeConfiguration<Opening>
{
    public void Configure(EntityTypeBuilder<Opening> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Openings");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(150).IsRequired();
        builder.Property(o => o.WidthFeet).HasPrecision(18, 4);
        builder.Property(o => o.HeightFeet).HasPrecision(18, 4);

        builder.HasOne(o => o.Surface).WithMany(s => s.Openings)
            .HasForeignKey(o => o.SurfaceId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.OrganizationId, o.SurfaceId });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Openings_Dimensions", "[WidthFeet] > 0 AND [HeightFeet] > 0");
            t.HasCheckConstraint("CK_Openings_Quantity", "[Quantity] > 0");
        });
    }
}

public sealed class ProjectNoteConfiguration : IEntityTypeConfiguration<ProjectNote>
{
    public void Configure(EntityTypeBuilder<ProjectNote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProjectNotes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();

        builder.HasOne<Project>().WithMany(p => p.Notes)
            .HasForeignKey(n => n.ProjectId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.OrganizationId, n.ProjectId });
    }
}

public sealed class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProjectDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);

        builder.HasOne<Project>().WithMany(p => p.Documents)
            .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.OrganizationId, d.ProjectId });
    }
}
