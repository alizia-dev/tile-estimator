using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Application.Engines.Takeoff;

/// <summary>
/// The single implementation of SPEC 14's takeoff formulas:
/// <code>
/// Floor area       = Length x Width
/// Wall area        = Length x Height
/// Opening area     = Width x Height x Quantity
/// Net area         = Gross - deductions + additions
/// Adjusted area    = Net x (1 + Waste%)
/// Boxes (calc)     = Adjusted / SqFtPerBox
/// Boxes (purchase) = ceiling(Boxes calc)
/// Material qty     = Area / coverage per unit
/// </code>
/// </summary>
public sealed class TakeoffEngine : ITakeoffEngine
{
    public WasteResolution ResolveWaste(WasteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // The estimator always has the last word.
        if (input.EstimatorOverridePercentage.HasValue)
        {
            var pct = Validate(input.EstimatorOverridePercentage.Value);
            return new WasteResolution(pct, WasteSource.EstimatorOverride,
                $"Estimator override: {BreakdownBuilder.Number(pct)}%");
        }

        // A matching waste rule is more specific than the pattern's default.
        if (input.MatchedRulePercentage.HasValue)
        {
            var pct = Validate(input.MatchedRulePercentage.Value);
            var name = input.MatchedRuleName ?? "waste rule";
            return new WasteResolution(pct, WasteSource.WasteRule,
                $"Waste rule '{name}': {BreakdownBuilder.Number(pct)}%");
        }

        if (input.PatternDefaultPercentage.HasValue)
        {
            var pct = Validate(input.PatternDefaultPercentage.Value);
            var name = input.PatternName ?? "pattern";
            return new WasteResolution(pct, WasteSource.PatternDefault,
                $"Pattern '{name}' default: {BreakdownBuilder.Number(pct)}%");
        }

        // Nothing configured means no waste. We never invent an "industry standard" figure.
        return new WasteResolution(0m, WasteSource.None, "No waste configured: 0%");
    }

    public AreaResult CalculateArea(SurfaceTakeoffInput surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var gross = CalculateGrossArea(surface);

        var deduction = 0m;
        var addition = 0m;
        foreach (var opening in surface.Openings)
        {
            DomainException.Require(opening.WidthFeet > 0m, $"Opening '{opening.Name}' needs a width greater than zero.");
            DomainException.Require(opening.HeightFeet > 0m, $"Opening '{opening.Name}' needs a height greater than zero.");
            DomainException.Require(opening.Quantity > 0, $"Opening '{opening.Name}' needs a quantity of at least one.");

            var area = opening.WidthFeet * opening.HeightFeet * opening.Quantity;
            if (opening.AddsArea)
            {
                addition += area;
            }
            else
            {
                deduction += area;
            }
        }

        DomainException.Require(deduction <= gross,
            $"Openings on '{surface.Name}' deduct {BreakdownBuilder.Number(deduction)} SF from a surface of only {BreakdownBuilder.Number(gross)} SF.");

        var net = Rounding.Quantity(gross - deduction + addition);
        var waste = ResolveWaste(surface.Waste);
        var adjusted = Rounding.Quantity(net * (1m + waste.Percentage / 100m));

        var builder = new BreakdownBuilder()
            .Step("Gross area", gross, UnitOfMeasure.SquareFeet)
            .Step("Openings deducted", deduction, UnitOfMeasure.SquareFeet)
            .Step("Openings added", addition, UnitOfMeasure.SquareFeet)
            .Step("Net area", net, UnitOfMeasure.SquareFeet)
            .Step("Waste", waste.Percentage, "%")
            .Step("Adjusted area", adjusted, UnitOfMeasure.SquareFeet)
            .Formula($"{BreakdownBuilder.Number(net)} SF x (1 + {BreakdownBuilder.Number(waste.Percentage)}%) = {BreakdownBuilder.Number(adjusted)} SF");

        return new AreaResult(
            Rounding.Quantity(gross),
            Rounding.Quantity(deduction),
            Rounding.Quantity(addition),
            net,
            waste.Percentage,
            adjusted,
            waste,
            builder.Build());
    }

    public TileQuantityResult CalculateTileQuantity(decimal adjustedAreaSquareFeet, TileInput tile) =>
        TileQuantity(adjustedAreaSquareFeet, tile);

    private static TileQuantityResult TileQuantity(decimal adjustedAreaSquareFeet, TileInput tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        DomainException.Require(adjustedAreaSquareFeet >= 0m, "Adjusted area cannot be negative.");
        DomainException.Require(tile.CoverageSqFtPerTile > 0m, $"Tile '{tile.Description}' has no coverage per tile.");
        DomainException.Require(tile.SqFtPerBox > 0m, $"Tile '{tile.Description}' has no square feet per box.");

        var tilesCalculated = Rounding.Quantity(adjustedAreaSquareFeet / tile.CoverageSqFtPerTile);
        var boxesCalculated = Rounding.Quantity(adjustedAreaSquareFeet / tile.SqFtPerBox);
        var boxesToPurchase = Rounding.PurchaseUnits(boxesCalculated);

        var breakdown = new BreakdownBuilder()
            .Step("Adjusted area", adjustedAreaSquareFeet, UnitOfMeasure.SquareFeet)
            .Step("Square feet per box", tile.SqFtPerBox, UnitOfMeasure.SquareFeet)
            .Step("Boxes calculated", boxesCalculated, UnitOfMeasure.Box)
            .Step("Boxes to purchase", boxesToPurchase, UnitOfMeasure.Box)
            .Formula($"{BreakdownBuilder.Number(adjustedAreaSquareFeet)} SF / {BreakdownBuilder.Number(tile.SqFtPerBox)} SF per box = {BreakdownBuilder.Number(boxesCalculated)} -> {boxesToPurchase} boxes")
            .Build();

        return new TileQuantityResult(
            tilesCalculated,
            Rounding.PurchaseUnits(tilesCalculated),
            boxesCalculated,
            boxesToPurchase,
            breakdown);
    }

    public SurfaceTakeoffResult CalculateSurface(SurfaceTakeoffInput surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var area = CalculateArea(surface);
        var lines = new List<TakeoffLineResult>();
        var sortOrder = 0;

        if (surface.Tile is not null)
        {
            lines.Add(BuildTileLine(surface, area, surface.Tile, sortOrder++));
        }

        foreach (var item in surface.AssemblyItems.OrderBy(i => i.SortOrder))
        {
            // A tile item inside an assembly is already covered by the surface tile line above.
            if (item.UsesSurfaceTile)
            {
                continue;
            }

            lines.Add(item.IsLabor()
                ? BuildLaborLine(surface, area, item, sortOrder++)
                : BuildMaterialLine(surface, area, item, sortOrder++));
        }

        return new SurfaceTakeoffResult(surface.SurfaceId, surface.Name, area, lines);
    }

    public TakeoffResult Calculate(IReadOnlyCollection<SurfaceTakeoffInput> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        var results = surfaces.Select(CalculateSurface).ToList();
        var lines = results.SelectMany(r => r.Lines).ToList();

        return new TakeoffResult(
            results,
            lines,
            Rounding.Quantity(results.Sum(r => r.Area.NetAreaSquareFeet)),
            Rounding.Quantity(results.Sum(r => r.Area.AdjustedAreaSquareFeet)));
    }

    private static decimal CalculateGrossArea(SurfaceTakeoffInput surface)
    {
        if (surface.AreaOverrideSquareFeet.HasValue)
        {
            DomainException.Require(surface.AreaOverrideSquareFeet.Value > 0m,
                $"The area override on '{surface.Name}' must be greater than zero.");
            return surface.AreaOverrideSquareFeet.Value;
        }

        DomainException.Require(surface.LengthFeet > 0m, $"Surface '{surface.Name}' needs a length greater than zero.");

        // Floors are measured across the plan; walls are measured up from the floor.
        var isFloor = surface.SurfaceType is SurfaceType.Floor or SurfaceType.ShowerFloor;
        if (isFloor)
        {
            DomainException.Require(surface.WidthFeet is > 0m, $"Floor surface '{surface.Name}' needs a width greater than zero.");
            return surface.LengthFeet * surface.WidthFeet!.Value;
        }

        if (surface.SurfaceType is SurfaceType.Wall or SurfaceType.ShowerWall or SurfaceType.Backsplash)
        {
            DomainException.Require(surface.HeightFeet is > 0m, $"Wall surface '{surface.Name}' needs a height greater than zero.");
            return surface.LengthFeet * surface.HeightFeet!.Value;
        }

        // A custom surface takes whichever second dimension was supplied.
        var second = surface.WidthFeet ?? surface.HeightFeet;
        DomainException.Require(second is > 0m, $"Custom surface '{surface.Name}' needs a width or a height.");
        return surface.LengthFeet * second!.Value;
    }

    private static TakeoffLineResult BuildTileLine(SurfaceTakeoffInput surface, AreaResult area, TileInput tile, int sortOrder)
    {
        var quantity = TileQuantity(area.AdjustedAreaSquareFeet, tile);
        var boxesToPurchase = quantity.BoxesToPurchase;

        // Tile is priced per square foot but purchased by the box, so the billable amount is
        // the square footage of whole boxes, not the square footage actually installed.
        var purchasedSquareFeet = Rounding.Quantity(boxesToPurchase * tile.SqFtPerBox);

        var breakdown = new BreakdownBuilder()
            .Step("Net area", area.NetAreaSquareFeet, UnitOfMeasure.SquareFeet)
            .Step("Waste", area.WastePercentage, "%")
            .Step("Adjusted area", area.AdjustedAreaSquareFeet, UnitOfMeasure.SquareFeet)
            .Step("Boxes calculated", quantity.BoxesCalculated, UnitOfMeasure.Box)
            .Step("Boxes to purchase", boxesToPurchase, UnitOfMeasure.Box)
            .Step("Square feet purchased", purchasedSquareFeet, UnitOfMeasure.SquareFeet)
            .Formula($"{BreakdownBuilder.Number(area.AdjustedAreaSquareFeet)} SF / {BreakdownBuilder.Number(tile.SqFtPerBox)} SF per box = {BreakdownBuilder.Number(quantity.BoxesCalculated)} -> {boxesToPurchase} boxes = {BreakdownBuilder.Number(purchasedSquareFeet)} SF at {BreakdownBuilder.Money(tile.SellingPricePerSqFt)}/SF")
            .Build();

        return new TakeoffLineResult
        {
            SurfaceId = surface.SurfaceId,
            RoomId = surface.RoomId,
            Category = EstimateLineCategory.Tile,
            Description = tile.Description,
            Quantity = area.AdjustedAreaSquareFeet,
            PurchaseQuantity = purchasedSquareFeet,
            Unit = UnitOfMeasure.SquareFeet,
            WastePercentage = area.WastePercentage,
            UnitCost = tile.CostPerSqFt,
            UnitPrice = tile.SellingPricePerSqFt,
            TileId = tile.TileId,
            AssemblyId = surface.AssemblyId,
            NetAreaSquareFeet = area.NetAreaSquareFeet,
            AdjustedAreaSquareFeet = area.AdjustedAreaSquareFeet,
            Breakdown = breakdown,
            SortOrder = sortOrder
        };
    }

    private static TakeoffLineResult BuildMaterialLine(SurfaceTakeoffInput surface, AreaResult area,
        AssemblyItemInput item, int sortOrder)
    {
        var (quantity, formula) = DeriveQuantity(surface, area, item);
        var purchase = IsWholeUnit(item.Unit) ? Rounding.PurchaseUnits(quantity) : quantity;

        var breakdown = new BreakdownBuilder()
            .Step("Basis", BasisFor(surface, area, item), BasisUnit(item))
            .Step("Quantity", quantity, item.Unit)
            .Step("Purchase quantity", purchase, item.Unit)
            .Formula(formula)
            .Build();

        return new TakeoffLineResult
        {
            SurfaceId = surface.SurfaceId,
            RoomId = surface.RoomId,
            Category = EstimateLineCategory.Material,
            Description = item.Description,
            Quantity = quantity,
            PurchaseQuantity = purchase,
            Unit = item.Unit,
            WastePercentage = item.WasteOverridePercentage ?? area.WastePercentage,
            UnitCost = item.UnitCost,
            UnitPrice = item.UnitPrice,
            MaterialId = item.MaterialId,
            AssemblyId = surface.AssemblyId,
            NetAreaSquareFeet = area.NetAreaSquareFeet,
            AdjustedAreaSquareFeet = area.AdjustedAreaSquareFeet,
            Breakdown = breakdown,
            SortOrder = sortOrder
        };
    }

    private static TakeoffLineResult BuildLaborLine(SurfaceTakeoffInput surface, AreaResult area,
        AssemblyItemInput item, int sortOrder)
    {
        var (quantity, _) = DeriveQuantity(surface, area, item);

        decimal unitPriceCost;
        decimal billableQuantity;
        string unit;
        string formula;

        if (item.LaborMethod == LaborCalculationMethod.Productivity)
        {
            DomainException.Require(item.LaborProductivity is > 0m,
                $"Labor '{item.Description}' uses the productivity method but has no productivity.");

            // Quantity / Productivity gives hours; hours are then billed at the hourly rate.
            billableQuantity = Rounding.Quantity(quantity / item.LaborProductivity!.Value);
            unitPriceCost = item.UnitCost;
            unit = UnitOfMeasure.Hour;
            formula = $"{BreakdownBuilder.Number(quantity)} {item.Unit} / {BreakdownBuilder.Number(item.LaborProductivity.Value)} per hour = {BreakdownBuilder.Number(billableQuantity)} HR x {BreakdownBuilder.Money(item.UnitPrice)}/HR = {BreakdownBuilder.Money(Rounding.Money(billableQuantity * item.UnitPrice))}";
        }
        else
        {
            billableQuantity = quantity;
            unitPriceCost = item.UnitCost;
            unit = item.Unit;
            formula = $"{BreakdownBuilder.Number(quantity)} {item.Unit} x {BreakdownBuilder.Money(item.UnitPrice)}/{item.Unit} = {BreakdownBuilder.Money(Rounding.Money(quantity * item.UnitPrice))}";
        }

        var breakdown = new BreakdownBuilder()
            .Step("Work quantity", quantity, item.Unit)
            .Step("Billable quantity", billableQuantity, unit)
            .Formula(formula)
            .Build();

        return new TakeoffLineResult
        {
            SurfaceId = surface.SurfaceId,
            RoomId = surface.RoomId,
            Category = EstimateLineCategory.Labor,
            Description = item.Description,
            Quantity = billableQuantity,
            PurchaseQuantity = billableQuantity,
            Unit = unit,
            WastePercentage = 0m,
            UnitCost = unitPriceCost,
            UnitPrice = item.UnitPrice,
            LaborRateId = item.LaborRateId,
            AssemblyId = surface.AssemblyId,
            NetAreaSquareFeet = area.NetAreaSquareFeet,
            AdjustedAreaSquareFeet = area.AdjustedAreaSquareFeet,
            Breakdown = breakdown,
            SortOrder = sortOrder
        };
    }

    /// <summary>Turns an assembly item's quantity method into an actual number of units.</summary>
    private static (decimal Quantity, string Formula) DeriveQuantity(SurfaceTakeoffInput surface,
        AreaResult area, AssemblyItemInput item)
    {
        switch (item.QuantityMethod)
        {
            case QuantityMethod.PerArea:
            {
                var qty = Rounding.Quantity(area.AdjustedAreaSquareFeet * item.Factor);
                var formula = item.Factor == 1m
                    ? $"{BreakdownBuilder.Number(area.AdjustedAreaSquareFeet)} SF = {BreakdownBuilder.Number(qty)} {item.Unit}"
                    : $"{BreakdownBuilder.Number(area.AdjustedAreaSquareFeet)} SF x {BreakdownBuilder.Number(item.Factor)} = {BreakdownBuilder.Number(qty)} {item.Unit}";
                return (qty, formula);
            }

            case QuantityMethod.PerLinearFoot:
            {
                var qty = Rounding.Quantity(surface.TrimLinearFeet * item.Factor);
                return (qty, $"{BreakdownBuilder.Number(surface.TrimLinearFeet)} LF x {BreakdownBuilder.Number(item.Factor)} = {BreakdownBuilder.Number(qty)} {item.Unit}");
            }

            case QuantityMethod.PerEach:
            {
                DomainException.Require(item.FixedQuantity is > 0m,
                    $"'{item.Description}' uses the per-each method but has no fixed quantity.");
                var qty = Rounding.Quantity(item.FixedQuantity!.Value * item.Factor);
                return (qty, $"{BreakdownBuilder.Number(item.FixedQuantity.Value)} x {BreakdownBuilder.Number(item.Factor)} = {BreakdownBuilder.Number(qty)} {item.Unit}");
            }

            case QuantityMethod.PerCoverage:
            {
                DomainException.Require(item.Coverage is > 0m,
                    $"'{item.Description}' is derived from coverage, but no coverage is configured on the material or assembly item.");
                var qty = Rounding.Quantity(area.AdjustedAreaSquareFeet / item.Coverage!.Value * item.Factor);
                var formula = item.Factor == 1m
                    ? $"{BreakdownBuilder.Number(area.AdjustedAreaSquareFeet)} SF / {BreakdownBuilder.Number(item.Coverage.Value)} SF per {item.Unit} = {BreakdownBuilder.Number(qty)} {item.Unit}"
                    : $"{BreakdownBuilder.Number(area.AdjustedAreaSquareFeet)} SF / {BreakdownBuilder.Number(item.Coverage.Value)} SF per {item.Unit} x {BreakdownBuilder.Number(item.Factor)} = {BreakdownBuilder.Number(qty)} {item.Unit}";
                return (qty, formula);
            }

            default:
                throw new DomainException($"Unsupported quantity method '{item.QuantityMethod}'.");
        }
    }

    private static decimal BasisFor(SurfaceTakeoffInput surface, AreaResult area, AssemblyItemInput item) =>
        item.QuantityMethod switch
        {
            QuantityMethod.PerLinearFoot => surface.TrimLinearFeet,
            QuantityMethod.PerEach => item.FixedQuantity ?? 0m,
            _ => area.AdjustedAreaSquareFeet
        };

    private static string BasisUnit(AssemblyItemInput item) =>
        item.QuantityMethod switch
        {
            QuantityMethod.PerLinearFoot => UnitOfMeasure.LinearFeet,
            QuantityMethod.PerEach => UnitOfMeasure.Each,
            _ => UnitOfMeasure.SquareFeet
        };

    /// <summary>
    /// Bags, boxes, sheets and rolls come in whole units, so a calculated 3.2 means buying 4.
    /// Square and linear feet are cut to size and stay fractional.
    /// </summary>
    private static bool IsWholeUnit(string unit) => unit switch
    {
        UnitOfMeasure.SquareFeet or UnitOfMeasure.LinearFeet or UnitOfMeasure.Hour => false,
        _ => true
    };

    private static decimal Validate(decimal percentage)
    {
        DomainException.Require(percentage >= 0m, "Waste cannot be negative.");
        return Rounding.Quantity(percentage);
    }
}

internal static class AssemblyItemInputExtensions
{
    public static bool IsLabor(this AssemblyItemInput item) => item.LaborRateId.HasValue;
}
