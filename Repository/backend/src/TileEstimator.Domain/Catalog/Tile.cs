using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Catalog;

/// <summary>SPEC 10 tile. Organization-specific: every tenant keeps its own catalog and pricing.</summary>
public class Tile : TenantEntity
{
    public string Sku { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? Collection { get; private set; }
    public TileMaterialType MaterialType { get; private set; } = TileMaterialType.Porcelain;

    public decimal LengthInches { get; private set; }
    public decimal WidthInches { get; private set; }
    public decimal? ThicknessInches { get; private set; }

    /// <summary>Square feet covered by a single tile, derived from its face dimensions.</summary>
    public decimal CoverageSqFt { get; private set; }

    public int TilesPerBox { get; private set; }

    /// <summary>Square feet per box. Drives the box count in the takeoff engine.</summary>
    public decimal SqFtPerBox { get; private set; }

    public decimal CostPerSqFt { get; private set; }
    public decimal SellingPricePerSqFt { get; private set; }

    public string? Finish { get; private set; }
    public string? Color { get; private set; }
    public string? Description { get; private set; }
    public bool Active { get; private set; } = true;

    private Tile() { }

    public static Tile Create(Guid organizationId, string sku, string productName, TileMaterialType materialType,
        decimal lengthInches, decimal widthInches, int tilesPerBox, decimal costPerSqFt, decimal sellingPricePerSqFt)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(sku), "SKU is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(productName), "Product name is required.");
        DomainException.Require(lengthInches > 0m, "Tile length must be greater than zero.");
        DomainException.Require(widthInches > 0m, "Tile width must be greater than zero.");
        DomainException.Require(tilesPerBox > 0, "Tiles per box must be at least one.");
        DomainException.Require(costPerSqFt >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPricePerSqFt >= 0m, "Selling price cannot be negative.");

        var tile = new Tile
        {
            OrganizationId = organizationId,
            Sku = sku.Trim(),
            ProductName = productName.Trim(),
            MaterialType = materialType,
            LengthInches = Rounding.Quantity(lengthInches),
            WidthInches = Rounding.Quantity(widthInches),
            TilesPerBox = tilesPerBox,
            CostPerSqFt = Rounding.Money(costPerSqFt),
            SellingPricePerSqFt = Rounding.Money(sellingPricePerSqFt)
        };
        tile.RecalculateCoverage();
        return tile;
    }

    public void UpdateDetails(string? brand, string? collection, string? finish, string? color,
        string? description, decimal? thicknessInches, bool active)
    {
        Brand = brand?.Trim();
        Collection = collection?.Trim();
        Finish = finish?.Trim();
        Color = color?.Trim();
        Description = description;
        ThicknessInches = thicknessInches;
        Active = active;
    }

    public void UpdateDimensions(decimal lengthInches, decimal widthInches, int tilesPerBox)
    {
        DomainException.Require(lengthInches > 0m, "Tile length must be greater than zero.");
        DomainException.Require(widthInches > 0m, "Tile width must be greater than zero.");
        DomainException.Require(tilesPerBox > 0, "Tiles per box must be at least one.");

        LengthInches = Rounding.Quantity(lengthInches);
        WidthInches = Rounding.Quantity(widthInches);
        TilesPerBox = tilesPerBox;
        RecalculateCoverage();
    }

    public void UpdatePricing(decimal costPerSqFt, decimal sellingPricePerSqFt)
    {
        DomainException.Require(costPerSqFt >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPricePerSqFt >= 0m, "Selling price cannot be negative.");
        CostPerSqFt = Rounding.Money(costPerSqFt);
        SellingPricePerSqFt = Rounding.Money(sellingPricePerSqFt);
    }

    /// <summary>
    /// Overrides the derived box coverage for products whose box does not match the face
    /// dimensions exactly (mosaic sheets, mixed-size kits).
    /// </summary>
    public void OverrideSqFtPerBox(decimal sqFtPerBox)
    {
        DomainException.Require(sqFtPerBox > 0m, "Square feet per box must be greater than zero.");
        SqFtPerBox = Rounding.Quantity(sqFtPerBox);
    }

    public void SetSku(string sku)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(sku), "SKU is required.");
        Sku = sku.Trim();
    }

    public void SetProductName(string productName)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(productName), "Product name is required.");
        ProductName = productName.Trim();
    }

    public void SetMaterialType(TileMaterialType materialType) => MaterialType = materialType;

    private void RecalculateCoverage()
    {
        // 144 square inches to a square foot.
        CoverageSqFt = Rounding.Quantity(LengthInches * WidthInches / 144m);
        SqFtPerBox = Rounding.Quantity(CoverageSqFt * TilesPerBox);
    }
}
