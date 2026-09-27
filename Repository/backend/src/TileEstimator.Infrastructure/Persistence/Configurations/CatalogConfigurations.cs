using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TileEstimator.Domain.Catalog;

namespace TileEstimator.Infrastructure.Persistence.Configurations;

public sealed class TileConfiguration : IEntityTypeConfiguration<Tile>
{
    public void Configure(EntityTypeBuilder<Tile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Tiles");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Sku).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Brand).HasMaxLength(100);
        builder.Property(t => t.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Collection).HasMaxLength(100);
        builder.Property(t => t.Finish).HasMaxLength(50);
        builder.Property(t => t.Color).HasMaxLength(50);
        builder.Property(t => t.Description).HasMaxLength(2000);

        builder.Property(t => t.LengthInches).HasPrecision(18, 4);
        builder.Property(t => t.WidthInches).HasPrecision(18, 4);
        builder.Property(t => t.ThicknessInches).HasPrecision(18, 4);
        builder.Property(t => t.CoverageSqFt).HasPrecision(18, 4);
        builder.Property(t => t.SqFtPerBox).HasPrecision(18, 4);
        builder.Property(t => t.CostPerSqFt).HasPrecision(18, 2);
        builder.Property(t => t.SellingPricePerSqFt).HasPrecision(18, 2);

        builder.HasIndex(t => new { t.OrganizationId, t.Sku }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(t => new { t.OrganizationId, t.ProductName });
        builder.HasIndex(t => new { t.OrganizationId, t.Active });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Tiles_Dimensions", "[LengthInches] > 0 AND [WidthInches] > 0");
            t.HasCheckConstraint("CK_Tiles_Box", "[TilesPerBox] > 0 AND [SqFtPerBox] > 0");
            t.HasCheckConstraint("CK_Tiles_Pricing", "[CostPerSqFt] >= 0 AND [SellingPricePerSqFt] >= 0");
        });
    }
}

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Materials");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Sku).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Unit).HasMaxLength(16).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(2000);

        builder.Property(m => m.Coverage).HasPrecision(18, 4);
        builder.Property(m => m.Cost).HasPrecision(18, 2);
        builder.Property(m => m.SellingPrice).HasPrecision(18, 2);

        builder.HasOne(m => m.Supplier).WithMany()
            .HasForeignKey(m => m.SupplierId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.OrganizationId, m.Sku }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(m => new { m.OrganizationId, m.Category });
        builder.HasIndex(m => new { m.OrganizationId, m.Active });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Materials_Coverage", "[Coverage] IS NULL OR [Coverage] > 0");
            t.HasCheckConstraint("CK_Materials_Pricing", "[Cost] >= 0 AND [SellingPrice] >= 0");
        });
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactName).HasMaxLength(150);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Phone).HasMaxLength(40);
        builder.Property(s => s.AccountNumber).HasMaxLength(64);

        builder.OwnsOne(s => s.Address, a => AddressConfiguration.ConfigureAddress(a, "Address"));

        builder.HasIndex(s => new { s.OrganizationId, s.Name });
    }
}

public sealed class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SupplierProducts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.SupplierSku).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Cost).HasPrecision(18, 2);

        builder.HasOne(p => p.Supplier).WithMany(s => s.Products)
            .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tile>().WithMany()
            .HasForeignKey(p => p.TileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Material>().WithMany()
            .HasForeignKey(p => p.MaterialId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.OrganizationId, p.SupplierId });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SupplierProducts_OneTarget",
            "([TileId] IS NOT NULL AND [MaterialId] IS NULL) OR ([TileId] IS NULL AND [MaterialId] IS NOT NULL)"));
    }
}

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PriceLists");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);

        // A price list moves money, so it carries a concurrency token (SPEC 18).
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasOne(p => p.Supplier).WithMany()
            .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => new { p.OrganizationId, p.Name });
        builder.HasIndex(p => new { p.OrganizationId, p.EffectiveFrom });
    }
}

public sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PriceListItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Cost).HasPrecision(18, 2);
        builder.Property(i => i.SellingPrice).HasPrecision(18, 2);

        builder.HasOne(i => i.PriceList).WithMany(p => p.Items)
            .HasForeignKey(i => i.PriceListId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tile>().WithMany()
            .HasForeignKey(i => i.TileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Material>().WithMany()
            .HasForeignKey(i => i.MaterialId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.OrganizationId, i.PriceListId });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_PriceListItems_OneTarget",
            "([TileId] IS NOT NULL AND [MaterialId] IS NULL) OR ([TileId] IS NULL AND [MaterialId] IS NOT NULL)"));
    }
}
