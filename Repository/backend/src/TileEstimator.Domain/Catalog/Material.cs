using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Catalog;

/// <summary>
/// SPEC 10 material: thinset, grout, backer board, membrane and everything else that is not tile.
/// <see cref="Coverage"/> is what the takeoff engine divides area by to get a purchase quantity,
/// and it always comes from the organization's own catalog, never from a hard-coded constant.
/// </summary>
public class Material : TenantEntity
{
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public MaterialCategory Category { get; private set; } = MaterialCategory.Other;

    /// <summary>The unit this material is bought in: BAG, SHEET, ROLL, GAL, TUBE, EA.</summary>
    public string Unit { get; private set; } = UnitOfMeasure.Each;

    /// <summary>
    /// How much one purchase unit covers, in square feet for area-based materials or linear feet
    /// for trim. Null means the material is counted rather than derived from coverage.
    /// </summary>
    public decimal? Coverage { get; private set; }

    public decimal Cost { get; private set; }
    public decimal SellingPrice { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string? Description { get; private set; }
    public bool Active { get; private set; } = true;

    public Supplier? Supplier { get; private set; }

    private Material() { }

    public static Material Create(Guid organizationId, string sku, string name, MaterialCategory category,
        string unit, decimal? coverage, decimal cost, decimal sellingPrice)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(sku), "SKU is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Material name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(unit), "Unit is required.");
        DomainException.Require(coverage is null or > 0m, "Coverage must be greater than zero when supplied.");
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPrice >= 0m, "Selling price cannot be negative.");

        return new Material
        {
            OrganizationId = organizationId,
            Sku = sku.Trim(),
            Name = name.Trim(),
            Category = category,
            Unit = unit.Trim().ToUpperInvariant(),
            Coverage = coverage.HasValue ? Rounding.Quantity(coverage.Value) : null,
            Cost = Rounding.Money(cost),
            SellingPrice = Rounding.Money(sellingPrice)
        };
    }

    public void Update(string sku, string name, MaterialCategory category, string unit, decimal? coverage,
        decimal cost, decimal sellingPrice, Guid? supplierId, string? description, bool active)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(sku), "SKU is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Material name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(unit), "Unit is required.");
        DomainException.Require(coverage is null or > 0m, "Coverage must be greater than zero when supplied.");
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPrice >= 0m, "Selling price cannot be negative.");

        Sku = sku.Trim();
        Name = name.Trim();
        Category = category;
        Unit = unit.Trim().ToUpperInvariant();
        Coverage = coverage.HasValue ? Rounding.Quantity(coverage.Value) : null;
        Cost = Rounding.Money(cost);
        SellingPrice = Rounding.Money(sellingPrice);
        SupplierId = supplierId;
        Description = description;
        Active = active;
    }
}

/// <summary>SPEC 10 supplier. No external supplier APIs in the MVP.</summary>
public class Supplier : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? AccountNumber { get; private set; }
    public Address? Address { get; private set; }
    public bool Active { get; private set; } = true;

    public ICollection<SupplierProduct> Products { get; private set; } = new List<SupplierProduct>();

    private Supplier() { }

    public static Supplier Create(Guid organizationId, string name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Supplier name is required.");
        return new Supplier { OrganizationId = organizationId, Name = name.Trim() };
    }

    public void Update(string name, string? contactName, string? email, string? phone,
        string? accountNumber, Address? address, bool active)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Supplier name is required.");
        Name = name.Trim();
        ContactName = contactName?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        AccountNumber = accountNumber?.Trim();
        Address = address;
        Active = active;
    }
}

/// <summary>What a given supplier calls a catalog item, and what they charge for it.</summary>
public class SupplierProduct : TenantEntity
{
    public Guid SupplierId { get; private set; }
    public Guid? TileId { get; private set; }
    public Guid? MaterialId { get; private set; }
    public string SupplierSku { get; private set; } = string.Empty;
    public decimal Cost { get; private set; }
    public int? LeadTimeDays { get; private set; }
    public bool Active { get; private set; } = true;

    public Supplier? Supplier { get; private set; }

    private SupplierProduct() { }

    public static SupplierProduct Create(Guid organizationId, Guid supplierId, Guid? tileId, Guid? materialId,
        string supplierSku, decimal cost, int? leadTimeDays)
    {
        DomainException.Require(tileId.HasValue ^ materialId.HasValue,
            "A supplier product must reference exactly one tile or one material.");
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");

        return new SupplierProduct
        {
            OrganizationId = organizationId,
            SupplierId = supplierId,
            TileId = tileId,
            MaterialId = materialId,
            SupplierSku = supplierSku.Trim(),
            Cost = Rounding.Money(cost),
            LeadTimeDays = leadTimeDays
        };
    }

    public void Update(string supplierSku, decimal cost, int? leadTimeDays, bool active)
    {
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");
        SupplierSku = supplierSku.Trim();
        Cost = Rounding.Money(cost);
        LeadTimeDays = leadTimeDays;
        Active = active;
    }
}
