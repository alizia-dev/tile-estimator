using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Infrastructure.Persistence.Configurations;

public sealed class PatternConfiguration : IEntityTypeConfiguration<Pattern>
{
    public void Configure(EntityTypeBuilder<Pattern> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Patterns");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.DefaultWastePercentage).HasPrecision(18, 4);

        builder.HasIndex(p => new { p.OrganizationId, p.Name }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.ToTable(t => t.HasCheckConstraint("CK_Patterns_Waste", "[DefaultWastePercentage] >= 0"));
    }
}

public sealed class WasteRuleConfiguration : IEntityTypeConfiguration<WasteRule>
{
    public void Configure(EntityTypeBuilder<WasteRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WasteRules");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.WastePercentage).HasPrecision(18, 4);

        builder.HasOne(w => w.Pattern).WithMany()
            .HasForeignKey(w => w.PatternId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.OrganizationId, w.Active });
        builder.HasIndex(w => new { w.OrganizationId, w.PatternId });

        builder.ToTable(t => t.HasCheckConstraint("CK_WasteRules_Waste", "[WastePercentage] >= 0"));
    }
}

public sealed class LaborRateConfiguration : IEntityTypeConfiguration<LaborRate>
{
    public void Configure(EntityTypeBuilder<LaborRate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LaborRates");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).HasMaxLength(150).IsRequired();
        builder.Property(l => l.Trade).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Unit).HasMaxLength(16).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(1000);
        builder.Property(l => l.Rate).HasPrecision(18, 4);
        builder.Property(l => l.Productivity).HasPrecision(18, 4);

        builder.HasIndex(l => new { l.OrganizationId, l.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => new { l.OrganizationId, l.Active });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LaborRates_Rate", "[Rate] >= 0");
            // The productivity method divides by this value, so it can never be zero.
            t.HasCheckConstraint("CK_LaborRates_Productivity",
                "[CalculationMethod] <> 2 OR ([Productivity] IS NOT NULL AND [Productivity] > 0)");
        });
    }
}

public sealed class AssemblyConfiguration : IEntityTypeConfiguration<Assembly>
{
    public void Configure(EntityTypeBuilder<Assembly> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Assemblies");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);

        builder.HasIndex(a => new { a.OrganizationId, a.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => new { a.OrganizationId, a.Active });
    }
}

public sealed class AssemblyItemConfiguration : IEntityTypeConfiguration<AssemblyItem>
{
    public void Configure(EntityTypeBuilder<AssemblyItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AssemblyItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Unit).HasMaxLength(16);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.Factor).HasPrecision(18, 4);
        builder.Property(i => i.FixedQuantity).HasPrecision(18, 4);
        builder.Property(i => i.CoverageOverride).HasPrecision(18, 4);
        builder.Property(i => i.WasteOverridePercentage).HasPrecision(18, 4);

        builder.HasOne(i => i.Assembly).WithMany(a => a.Items)
            .HasForeignKey(i => i.AssemblyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Material).WithMany()
            .HasForeignKey(i => i.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.LaborRate).WithMany()
            .HasForeignKey(i => i.LaborRateId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.OrganizationId, i.AssemblyId });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AssemblyItems_Factor", "[Factor] > 0");
            // Exactly one of: a material, a labor rate, or the surface's own tile.
            t.HasCheckConstraint("CK_AssemblyItems_OneTarget",
                "(CASE WHEN [MaterialId] IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN [LaborRateId] IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN [UsesSurfaceTile] = 1 THEN 1 ELSE 0 END) = 1");
        });
    }
}

public sealed class TakeoffConfiguration : IEntityTypeConfiguration<Takeoff>
{
    public void Configure(EntityTypeBuilder<Takeoff> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Takeoffs");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(4000);
        builder.Property(t => t.TotalNetAreaSquareFeet).HasPrecision(18, 4);
        builder.Property(t => t.TotalAdjustedAreaSquareFeet).HasPrecision(18, 4);

        builder.HasOne(t => t.Project).WithMany()
            .HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.OrganizationId, t.ProjectId });
    }
}

public sealed class TakeoffItemConfiguration : IEntityTypeConfiguration<TakeoffItem>
{
    public void Configure(EntityTypeBuilder<TakeoffItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TakeoffItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(16).IsRequired();
        builder.Property(i => i.CalculationReference).HasMaxLength(1000);

        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.Property(i => i.PurchaseQuantity).HasPrecision(18, 4);
        builder.Property(i => i.WastePercentage).HasPrecision(18, 4);
        builder.Property(i => i.NetAreaSquareFeet).HasPrecision(18, 4);
        builder.Property(i => i.AdjustedAreaSquareFeet).HasPrecision(18, 4);

        builder.HasOne(i => i.Takeoff).WithMany(t => t.Items)
            .HasForeignKey(i => i.TakeoffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Room>().WithMany().HasForeignKey(i => i.RoomId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Surface>().WithMany().HasForeignKey(i => i.SurfaceId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => new { i.OrganizationId, i.TakeoffId });
    }
}

public sealed class EstimateConfiguration : IEntityTypeConfiguration<Estimate>
{
    public void Configure(EntityTypeBuilder<Estimate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Estimates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EstimateNumber).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(4000);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();

        // Pricing configuration snapshot: percentages keep 4 dp, money keeps 2.
        builder.Property(e => e.MarkupPercentage).HasPrecision(18, 4);
        builder.Property(e => e.MarginPercentage).HasPrecision(18, 4);
        builder.Property(e => e.OverheadPercentage).HasPrecision(18, 4);
        builder.Property(e => e.DiscountValue).HasPrecision(18, 4);
        builder.Property(e => e.TaxRatePercentage).HasPrecision(18, 4);
        builder.Property(e => e.FixedMarkupAmount).HasPrecision(18, 2);

        foreach (var money in new[]
                 {
                     nameof(Estimate.MaterialCost), nameof(Estimate.LaborCost), nameof(Estimate.OtherCost),
                     nameof(Estimate.OverheadAmount), nameof(Estimate.TotalCost), nameof(Estimate.MarkupAmount),
                     nameof(Estimate.DiscountAmount), nameof(Estimate.TaxAmount), nameof(Estimate.Subtotal),
                     nameof(Estimate.GrandTotal)
                 })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        // Two estimators must not silently overwrite each other's numbers (SPEC 18).
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Project).WithMany()
            .HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Estimate>().WithMany()
            .HasForeignKey(e => e.SupersedesEstimateId).OnDelete(DeleteBehavior.NoAction);

        // EST-1001 v1 and EST-1001 v2 coexist; the same version never does.
        builder.HasIndex(e => new { e.OrganizationId, e.EstimateNumber, e.Version })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.OrganizationId, e.ProjectId });
        builder.HasIndex(e => new { e.OrganizationId, e.Status });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Estimates_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_Estimates_Margin", "[MarginPercentage] >= 0 AND [MarginPercentage] < 100");
            t.HasCheckConstraint("CK_Estimates_Markup", "[MarkupPercentage] >= 0");
            t.HasCheckConstraint("CK_Estimates_Tax", "[TaxRatePercentage] >= 0");
        });
    }
}

public sealed class EstimateLineConfiguration : IEntityTypeConfiguration<EstimateLine>
{
    public void Configure(EntityTypeBuilder<EstimateLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EstimateLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Unit).HasMaxLength(16).IsRequired();
        builder.Property(l => l.CalculationReference).HasMaxLength(1000);
        builder.Property(l => l.OverrideReason).HasMaxLength(500);
        builder.Property(l => l.Notes).HasMaxLength(2000);

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.PurchaseQuantity).HasPrecision(18, 4);
        builder.Property(l => l.WastePercentage).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 2);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.OriginalUnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.TotalCost).HasPrecision(18, 2);
        builder.Property(l => l.TotalPrice).HasPrecision(18, 2);

        builder.HasOne(l => l.Estimate).WithMany(e => e.Lines)
            .HasForeignKey(l => l.EstimateId).OnDelete(DeleteBehavior.Cascade);

        // Catalog links are for reporting only. They are NoAction so deleting a tile can never
        // rewrite or remove a historical estimate line.
        builder.HasOne<Room>().WithMany().HasForeignKey(l => l.RoomId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Surface>().WithMany().HasForeignKey(l => l.SurfaceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Tile>().WithMany().HasForeignKey(l => l.TileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Material>().WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<LaborRate>().WithMany().HasForeignKey(l => l.LaborRateId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(l => new { l.OrganizationId, l.EstimateId });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_EstimateLines_NonNegative",
            "[Quantity] >= 0 AND [PurchaseQuantity] >= 0 AND [UnitCost] >= 0 AND [UnitPrice] >= 0"));
    }
}
