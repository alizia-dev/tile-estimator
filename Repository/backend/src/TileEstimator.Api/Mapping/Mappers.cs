using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Contracts.Catalog;
using TileEstimator.Contracts.Estimating;
using TileEstimator.Contracts.Projects;
using TileEstimator.Contracts.Quoting;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.Quotes;

namespace TileEstimator.Api.Mapping;

/// <summary>
/// Maps domain entities to the DTOs in Contracts. EF entities are never returned directly
/// (SPEC 23), so every response the API produces passes through here.
/// </summary>
public static class Mappers
{
    public static AddressResponse? ToResponse(this Address? address) =>
        address is null
            ? null
            : new AddressResponse(address.Line1, address.Line2, address.City, address.State,
                address.PostalCode, address.Country, address.SingleLine);

    public static Address? ToDomain(this AddressRequest? request) =>
        request is null
            ? null
            : Address.Create(request.Line1, request.Line2, request.City, request.State,
                request.PostalCode, request.Country);

    // --- Catalog ---------------------------------------------------------------------------------

    public static TileResponse ToResponse(this Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return new TileResponse(tile.Id, tile.Sku, tile.Brand, tile.ProductName, tile.Collection,
            tile.MaterialType.ToString(), tile.LengthInches, tile.WidthInches, tile.ThicknessInches,
            tile.CoverageSqFt, tile.TilesPerBox, tile.SqFtPerBox, tile.CostPerSqFt,
            tile.SellingPricePerSqFt, tile.Finish, tile.Color, tile.Description, tile.Active);
    }

    public static MaterialResponse ToResponse(this Material material, string? supplierName = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new MaterialResponse(material.Id, material.Sku, material.Name,
            material.Category.ToString(), material.Unit, material.Coverage, material.Cost,
            material.SellingPrice, material.SupplierId, supplierName, material.Description, material.Active);
    }

    public static PatternResponse ToResponse(this Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return new PatternResponse(pattern.Id, pattern.Name, pattern.Description,
            pattern.DefaultWastePercentage, pattern.Active, pattern.SortOrder);
    }

    public static WasteRuleResponse ToResponse(this WasteRule rule, string? patternName = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return new WasteRuleResponse(rule.Id, rule.Name, rule.PatternId, patternName,
            rule.SurfaceType?.ToString(), rule.TileMaterialType?.ToString(), rule.RoomType?.ToString(),
            rule.WastePercentage, rule.Priority, rule.Active);
    }

    public static LaborRateResponse ToResponse(this LaborRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);
        return new LaborRateResponse(rate.Id, rate.Name, rate.Trade, rate.Unit,
            rate.CalculationMethod.ToString(), rate.Rate, rate.Productivity, rate.Description, rate.Active);
    }

    public static SupplierResponse ToResponse(this Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        return new SupplierResponse(supplier.Id, supplier.Name, supplier.ContactName,
            supplier.Email, supplier.Phone, supplier.AccountNumber, supplier.Active);
    }

    public static AssemblyResponse ToResponse(this Assembly assembly,
        IReadOnlyDictionary<Guid, string>? materialNames = null,
        IReadOnlyDictionary<Guid, string>? laborNames = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new AssemblyResponse(assembly.Id, assembly.Name, assembly.Description,
            assembly.AppliesToSurfaceType?.ToString(), assembly.Active,
            assembly.Items.OrderBy(i => i.SortOrder).Select(i => new AssemblyItemResponse(
                i.Id,
                i.MaterialId,
                i.MaterialId.HasValue ? Lookup(materialNames, i.MaterialId.Value) : null,
                i.LaborRateId,
                i.LaborRateId.HasValue ? Lookup(laborNames, i.LaborRateId.Value) : null,
                i.UsesSurfaceTile,
                i.QuantityMethod.ToString(),
                i.Unit,
                i.Factor,
                i.FixedQuantity,
                i.CoverageOverride,
                i.WasteOverridePercentage,
                i.Description,
                i.SortOrder)).ToList());
    }

    // --- Customers and projects --------------------------------------------------------------------

    public static CustomerResponse ToResponse(this Customer customer, int projectCount = 0)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return new CustomerResponse(customer.Id, customer.CustomerNumber, customer.Type.ToString(),
            customer.DisplayName, customer.FirstName, customer.LastName, customer.CompanyName,
            customer.Email, customer.Phone, customer.Notes, customer.Status.ToString(),
            customer.BillingAddress.ToResponse(), customer.ServiceAddress.ToResponse(),
            projectCount, customer.CreatedAt);
    }

    public static ProjectResponse ToResponse(this Project project, string customerName,
        int roomCount = 0, decimal? latestEstimateTotal = null, string? latestEstimateNumber = null,
        string? latestQuoteStatus = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectResponse(project.Id, project.ProjectNumber, project.Name, project.Description,
            project.Type.ToString(), project.Status.ToString(), project.CustomerId, customerName,
            project.SiteAddress.ToResponse(), project.StartDate, project.EstimatedCompletionDate,
            roomCount, latestEstimateTotal, latestEstimateNumber, latestQuoteStatus,
            project.CreatedAt, project.UpdatedAt);
    }

    public static RoomResponse ToResponse(this Room room, CatalogNameLookup? names = null)
    {
        ArgumentNullException.ThrowIfNull(room);
        return new RoomResponse(room.Id, room.ProjectId, room.Name, room.Type.ToString(),
            room.CustomTypeName, room.Notes, room.SortOrder,
            room.Surfaces.OrderBy(s => s.SortOrder).Select(s => s.ToResponse(names)).ToList());
    }

    public static SurfaceResponse ToResponse(this Surface surface, CatalogNameLookup? names = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return new SurfaceResponse(surface.Id, surface.RoomId, surface.Name, surface.Type.ToString(),
            surface.LengthFeet, surface.WidthFeet, surface.HeightFeet, surface.AreaOverrideSquareFeet,
            surface.TrimLinearFeet,
            surface.TileId, surface.TileId.HasValue ? Lookup(names?.Tiles, surface.TileId.Value) : null,
            surface.PatternId, surface.PatternId.HasValue ? Lookup(names?.Patterns, surface.PatternId.Value) : null,
            surface.AssemblyId, surface.AssemblyId.HasValue ? Lookup(names?.Assemblies, surface.AssemblyId.Value) : null,
            surface.WasteOverridePercentage, surface.Notes, surface.SortOrder,
            surface.Openings.Select(o => o.ToResponse()).ToList());
    }

    public static OpeningResponse ToResponse(this Opening opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        return new OpeningResponse(opening.Id, opening.SurfaceId, opening.Name, opening.Type.ToString(),
            opening.WidthFeet, opening.HeightFeet, opening.Quantity, opening.AddsArea,
            opening.TotalAreaSquareFeet);
    }

    // --- Takeoff ------------------------------------------------------------------------------------

    public static TakeoffLineResponse ToResponse(this TakeoffLineResult line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new TakeoffLineResponse(line.SurfaceId, line.RoomId, line.Category.ToString(),
            line.Description, line.Quantity, line.PurchaseQuantity, line.Unit, line.WastePercentage,
            line.UnitCost, line.UnitPrice, line.TotalCost, line.TotalPrice, line.Breakdown.Formula);
    }

    public static TakeoffResponse ToResponse(this TakeoffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var surfaces = result.Surfaces.Select(s => new SurfaceAreaResponse(
            s.SurfaceId, s.SurfaceName,
            s.Area.GrossAreaSquareFeet, s.Area.OpeningDeductionSquareFeet, s.Area.OpeningAdditionSquareFeet,
            s.Area.NetAreaSquareFeet, s.Area.WastePercentage, s.Area.Waste.Source.ToString(),
            s.Area.Waste.Explanation, s.Area.AdjustedAreaSquareFeet, s.Area.Breakdown.Formula,
            s.Lines.Select(ToResponse).ToList())).ToList();

        var lines = result.Lines.Select(ToResponse).ToList();

        return new TakeoffResponse(
            surfaces, lines,
            result.TotalNetAreaSquareFeet, result.TotalAdjustedAreaSquareFeet,
            lines.Where(l => l.Category is "Tile" or "Material").Sum(l => l.TotalCost),
            lines.Where(l => l.Category == "Labor").Sum(l => l.TotalCost),
            lines.Sum(l => l.TotalCost),
            lines.Sum(l => l.TotalPrice));
    }

    // --- Estimates ------------------------------------------------------------------------------------

    public static EstimateResponse ToResponse(this Estimate estimate, string projectName,
        IReadOnlyDictionary<Guid, string>? roomNames = null)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        var grossProfit = estimate.Subtotal - estimate.DiscountAmount - estimate.TotalCost;
        var netRevenue = estimate.Subtotal - estimate.DiscountAmount;
        var grossMargin = netRevenue == 0m ? 0m : decimal.Round(grossProfit / netRevenue * 100m, 4);

        return new EstimateResponse(
            estimate.Id, estimate.ProjectId, projectName, estimate.EstimateNumber, estimate.Version,
            estimate.DisplayNumber, estimate.SupersedesEstimateId, estimate.Status.ToString(),
            estimate.Title, estimate.Notes, estimate.Currency,
            estimate.PricingStrategy.ToString(), estimate.MarkupPercentage, estimate.MarginPercentage,
            estimate.FixedMarkupAmount, estimate.OverheadPercentage, estimate.DiscountType.ToString(),
            estimate.DiscountValue, estimate.TaxRatePercentage, estimate.TaxBasis.ToString(),
            estimate.DiscountBeforeTax,
            estimate.MaterialCost, estimate.LaborCost, estimate.OtherCost, estimate.OverheadAmount,
            estimate.TotalCost, estimate.MarkupAmount, estimate.DiscountAmount, estimate.TaxAmount,
            estimate.Subtotal, estimate.GrandTotal, grossProfit, grossMargin,
            estimate.IsEditable, estimate.FinalizedAt, estimate.CreatedAt, estimate.UpdatedAt,
            EncodeRowVersion(estimate.RowVersion),
            estimate.Lines.OrderBy(l => l.SortOrder).Select(l => l.ToResponse(roomNames)).ToList());
    }

    public static EstimateLineResponse ToResponse(this EstimateLine line,
        IReadOnlyDictionary<Guid, string>? roomNames = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new EstimateLineResponse(line.Id, line.RoomId,
            line.RoomId.HasValue ? Lookup(roomNames, line.RoomId.Value) : null,
            line.SurfaceId, line.Category.ToString(), line.Description,
            line.Quantity, line.PurchaseQuantity, line.Unit, line.UnitCost, line.UnitPrice,
            line.WastePercentage, line.TotalCost, line.TotalPrice, line.CalculationReference,
            line.IsPriceOverridden, line.OriginalUnitPrice, line.OverrideReason, line.Notes, line.SortOrder);
    }

    // --- Quotes -----------------------------------------------------------------------------------------

    public static QuoteResponse ToResponse(this Quote quote, string projectName, string estimateNumber)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return new QuoteResponse(
            quote.Id, quote.QuoteNumber, quote.Version, quote.Status.ToString(),
            quote.ProjectId, projectName, quote.EstimateId, estimateNumber,
            quote.CustomerId, quote.CustomerDisplayName, quote.CustomerEmail,
            quote.Title, quote.ScopeOfWork, quote.TermsAndConditions, quote.Currency,
            quote.QuoteDate, quote.ExpiresAt,
            quote.MaterialTotal, quote.LaborTotal, quote.OtherTotal, quote.Subtotal,
            quote.DiscountAmount, quote.TaxAmount, quote.GrandTotal,
            quote.SentAt, quote.FirstViewedAt, quote.RespondedAt,
            !string.IsNullOrWhiteSpace(quote.PdfStorageKey),
            quote.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLineResponse(
                l.Id, l.RoomName, l.Category.ToString(), l.Description, l.Quantity, l.Unit,
                l.UnitPrice, l.TotalPrice, l.IsVisibleToCustomer, l.SortOrder)).ToList(),
            quote.Recipients.Select(r => new QuoteRecipientResponse(
                r.Id, r.Email, r.Name, r.SentAt, r.DeliveryError)).ToList(),
            quote.Approvals.OrderByDescending(a => a.DecidedAt).Select(a => new QuoteApprovalResponse(
                a.Id, a.Decision.ToString(), a.CustomerName, a.CustomerEmail,
                a.Comments, a.DecidedAt)).ToList());
    }

    /// <summary>
    /// The public view. Cost, margin and internal ids are deliberately absent, so the page
    /// cannot leak what the contractor pays or be used to reach anything else.
    /// </summary>
    public static PublicQuoteResponse ToPublicResponse(this Quote quote, string? projectName,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var expired = quote.IsExpired(utcNow);
        var canRespond = !expired &&
                         quote.Status is Domain.Enums.QuoteStatus.Sent or Domain.Enums.QuoteStatus.Viewed;

        var latest = quote.Approvals.OrderByDescending(a => a.DecidedAt).FirstOrDefault();

        return new PublicQuoteResponse(
            quote.QuoteNumber, quote.Status.ToString(), quote.Title, quote.ScopeOfWork,
            quote.TermsAndConditions, quote.FooterNote,
            quote.CompanyName, quote.CompanyEmail, quote.CompanyPhone, quote.CompanyAddressLine,
            quote.CompanyLicenseNumber,
            quote.CustomerDisplayName, projectName, quote.Currency,
            quote.QuoteDate, quote.ExpiresAt, expired, canRespond,
            quote.MaterialTotal, quote.LaborTotal, quote.OtherTotal, quote.Subtotal,
            quote.DiscountAmount, quote.TaxAmount, quote.GrandTotal,
            quote.Lines.Where(l => l.IsVisibleToCustomer).OrderBy(l => l.SortOrder)
                .Select(l => new PublicQuoteLineResponse(l.RoomName, l.Description, l.Quantity,
                    l.Unit, l.UnitPrice, l.TotalPrice)).ToList(),
            latest is null
                ? null
                : new PublicQuoteDecisionResponse(latest.Decision.ToString(), latest.CustomerName,
                    latest.DecidedAt));
    }

    public static ChangeOrderResponse ToResponse(this ChangeOrder changeOrder)
    {
        ArgumentNullException.ThrowIfNull(changeOrder);
        return new ChangeOrderResponse(changeOrder.Id, changeOrder.ProjectId, changeOrder.QuoteId,
            changeOrder.Number, changeOrder.Description, changeOrder.Reason, changeOrder.Amount,
            changeOrder.Status.ToString(), changeOrder.ApprovedAt, changeOrder.CreatedAt);
    }

    /// <summary>Concurrency tokens travel as base64 so they survive JSON round-tripping.</summary>
    public static string EncodeRowVersion(byte[]? rowVersion) =>
        rowVersion is null ? string.Empty : Convert.ToBase64String(rowVersion);

    public static byte[]? DecodeRowVersion(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        return Convert.TryFromBase64String(encoded, new byte[encoded.Length], out _)
            ? Convert.FromBase64String(encoded)
            : null;
    }

    private static string? Lookup(IReadOnlyDictionary<Guid, string>? names, Guid id) =>
        names is not null && names.TryGetValue(id, out var name) ? name : null;
}

/// <summary>Display names for catalog rows a response references, loaded once per request.</summary>
public sealed record CatalogNameLookup(
    IReadOnlyDictionary<Guid, string> Tiles,
    IReadOnlyDictionary<Guid, string> Patterns,
    IReadOnlyDictionary<Guid, string> Assemblies);
