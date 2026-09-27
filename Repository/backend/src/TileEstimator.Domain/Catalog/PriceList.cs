using TileEstimator.Domain.Common;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Catalog;

/// <summary>
/// SPEC 10 price list: a named, dated set of prices an organization can apply, for example a
/// contractor tier or a seasonal supplier sheet. Carries a rowversion because it moves money.
/// </summary>
public class PriceList : TenantEntity, IHasRowVersion
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? SupplierId { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsDefault { get; private set; }
    public bool Active { get; private set; } = true;

    public byte[]? RowVersion { get; set; }

    public Supplier? Supplier { get; private set; }
    public ICollection<PriceListItem> Items { get; private set; } = new List<PriceListItem>();

    private PriceList() { }

    public static PriceList Create(Guid organizationId, string name, DateTime effectiveFrom,
        DateTime? effectiveTo, Guid? supplierId)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Price list name is required.");
        DomainException.Require(effectiveTo is null || effectiveTo > effectiveFrom,
            "The end of a price list must come after its start.");

        return new PriceList
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SupplierId = supplierId
        };
    }

    public void Update(string name, string? description, DateTime effectiveFrom, DateTime? effectiveTo,
        Guid? supplierId, bool isDefault, bool active)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Price list name is required.");
        DomainException.Require(effectiveTo is null || effectiveTo > effectiveFrom,
            "The end of a price list must come after its start.");

        Name = name.Trim();
        Description = description;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SupplierId = supplierId;
        IsDefault = isDefault;
        Active = active;
    }

    public bool IsEffectiveOn(DateTime utcDate) =>
        Active && EffectiveFrom <= utcDate && (EffectiveTo is null || EffectiveTo > utcDate);
}

/// <summary>One priced row in a price list, pointing at either a tile or a material.</summary>
public class PriceListItem : TenantEntity
{
    public Guid PriceListId { get; private set; }
    public Guid? TileId { get; private set; }
    public Guid? MaterialId { get; private set; }
    public decimal Cost { get; private set; }
    public decimal? SellingPrice { get; private set; }

    public PriceList? PriceList { get; private set; }

    private PriceListItem() { }

    public static PriceListItem Create(Guid organizationId, Guid priceListId, Guid? tileId, Guid? materialId,
        decimal cost, decimal? sellingPrice)
    {
        DomainException.Require(tileId.HasValue ^ materialId.HasValue,
            "A price list item must reference exactly one tile or one material.");
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPrice is null or >= 0m, "Selling price cannot be negative.");

        return new PriceListItem
        {
            OrganizationId = organizationId,
            PriceListId = priceListId,
            TileId = tileId,
            MaterialId = materialId,
            Cost = Rounding.Money(cost),
            SellingPrice = sellingPrice.HasValue ? Rounding.Money(sellingPrice.Value) : null
        };
    }

    public void UpdatePricing(decimal cost, decimal? sellingPrice)
    {
        DomainException.Require(cost >= 0m, "Cost cannot be negative.");
        DomainException.Require(sellingPrice is null or >= 0m, "Selling price cannot be negative.");
        Cost = Rounding.Money(cost);
        SellingPrice = sellingPrice.HasValue ? Rounding.Money(sellingPrice.Value) : null;
    }
}
