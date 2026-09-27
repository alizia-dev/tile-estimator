using FluentAssertions;
using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;
using Xunit;

namespace TileEstimator.UnitTests.Engines;

public class TakeoffEngineTests
{
    private readonly TakeoffEngine _engine = new();

    private static TileInput Tile(decimal sqFtPerBox = 10m, decimal coveragePerTile = 1m,
        decimal cost = 2m, decimal price = 4m) =>
        new(Guid.NewGuid(), "12x12 Porcelain", coveragePerTile, sqFtPerBox, cost, price);

    private static SurfaceTakeoffInput Floor(decimal length, decimal width, decimal? waste = null,
        IReadOnlyList<OpeningInput>? openings = null, TileInput? tile = null) => new()
        {
            Name = "Floor",
            SurfaceType = SurfaceType.Floor,
            LengthFeet = length,
            WidthFeet = width,
            Openings = openings ?? [],
            Tile = tile,
            Waste = new WasteInput(null, null, waste)
        };

    // --- The SPEC 14 reference case ------------------------------------------------------

    [Fact]
    public void ReferenceCase_12x15_with_12_percent_waste_needs_21_boxes()
    {
        var tile = Tile(sqFtPerBox: 10m);
        var surface = Floor(12m, 15m, waste: 12m, tile: tile);

        var area = _engine.CalculateArea(surface);

        area.GrossAreaSquareFeet.Should().Be(180m);
        area.NetAreaSquareFeet.Should().Be(180m);
        area.AdjustedAreaSquareFeet.Should().Be(201.6m);

        var quantity = _engine.CalculateTileQuantity(area.AdjustedAreaSquareFeet, tile);

        quantity.BoxesCalculated.Should().Be(20.16m);
        quantity.BoxesToPurchase.Should().Be(21);
    }

    // --- Areas ---------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 12, 120)]
    [InlineData(8.5, 10.25, 87.125)]
    [InlineData(1, 1, 1)]
    public void Floor_area_is_length_times_width(decimal length, decimal width, decimal expected) =>
        _engine.CalculateArea(Floor(length, width)).GrossAreaSquareFeet.Should().Be(expected);

    [Fact]
    public void Wall_area_is_length_times_height()
    {
        var wall = new SurfaceTakeoffInput
        {
            Name = "North wall",
            SurfaceType = SurfaceType.Wall,
            LengthFeet = 10m,
            HeightFeet = 8m,
            Waste = new WasteInput(null, null, null)
        };

        _engine.CalculateArea(wall).GrossAreaSquareFeet.Should().Be(80m);
    }

    [Fact]
    public void Area_override_replaces_the_dimension_calculation()
    {
        var surface = Floor(10m, 10m) with { AreaOverrideSquareFeet = 63.5m };

        _engine.CalculateArea(surface).GrossAreaSquareFeet.Should().Be(63.5m);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Zero_or_negative_dimensions_are_rejected(decimal length, decimal width)
    {
        var act = () => _engine.CalculateArea(Floor(length, width));

        act.Should().Throw<DomainException>();
    }

    // --- Openings ------------------------------------------------------------------------

    [Fact]
    public void Openings_are_deducted_from_the_gross_area()
    {
        var openings = new[]
        {
            new OpeningInput("Door", OpeningType.Door, 3m, 7m, 1, AddsArea: false),
            new OpeningInput("Window", OpeningType.Window, 4m, 3m, 2, AddsArea: false)
        };
        var surface = new SurfaceTakeoffInput
        {
            Name = "Wall",
            SurfaceType = SurfaceType.Wall,
            LengthFeet = 20m,
            HeightFeet = 8m,
            Openings = openings,
            Waste = new WasteInput(null, null, null)
        };

        var area = _engine.CalculateArea(surface);

        area.GrossAreaSquareFeet.Should().Be(160m);
        area.OpeningDeductionSquareFeet.Should().Be(45m); // 21 + 24
        area.NetAreaSquareFeet.Should().Be(115m);
    }

    [Fact]
    public void A_niche_adds_area_because_it_gets_tiled()
    {
        var openings = new[] { new OpeningInput("Niche", OpeningType.Niche, 2m, 1.5m, 1, AddsArea: true) };
        var surface = new SurfaceTakeoffInput
        {
            Name = "Shower wall",
            SurfaceType = SurfaceType.ShowerWall,
            LengthFeet = 5m,
            HeightFeet = 8m,
            Openings = openings,
            Waste = new WasteInput(null, null, null)
        };

        var area = _engine.CalculateArea(surface);

        area.OpeningAdditionSquareFeet.Should().Be(3m);
        area.NetAreaSquareFeet.Should().Be(43m);
    }

    [Fact]
    public void Openings_larger_than_the_surface_are_rejected()
    {
        var openings = new[] { new OpeningInput("Opening", OpeningType.Other, 20m, 20m, 1, AddsArea: false) };
        var surface = Floor(10m, 10m, openings: openings);

        var act = () => _engine.CalculateArea(surface);

        act.Should().Throw<DomainException>().WithMessage("*deduct*");
    }

    // --- Waste ---------------------------------------------------------------------------

    [Fact]
    public void Zero_waste_leaves_the_area_untouched()
    {
        var area = _engine.CalculateArea(Floor(10m, 10m, waste: 0m));

        area.AdjustedAreaSquareFeet.Should().Be(100m);
        area.WastePercentage.Should().Be(0m);
    }

    [Fact]
    public void Waste_resolution_prefers_the_estimator_override()
    {
        var resolution = _engine.ResolveWaste(new WasteInput(10m, 15m, 20m));

        resolution.Percentage.Should().Be(20m);
        resolution.Source.Should().Be(WasteSource.EstimatorOverride);
    }

    [Fact]
    public void Waste_resolution_prefers_a_matching_rule_over_the_pattern_default()
    {
        var resolution = _engine.ResolveWaste(new WasteInput(10m, 15m, null));

        resolution.Percentage.Should().Be(15m);
        resolution.Source.Should().Be(WasteSource.WasteRule);
    }

    [Fact]
    public void Waste_resolution_falls_back_to_the_pattern_default()
    {
        var resolution = _engine.ResolveWaste(new WasteInput(10m, null, null, PatternName: "Herringbone"));

        resolution.Percentage.Should().Be(10m);
        resolution.Source.Should().Be(WasteSource.PatternDefault);
    }

    [Fact]
    public void With_nothing_configured_waste_is_zero_and_never_an_invented_default()
    {
        var resolution = _engine.ResolveWaste(new WasteInput(null, null, null));

        resolution.Percentage.Should().Be(0m);
        resolution.Source.Should().Be(WasteSource.None);
    }

    [Fact]
    public void Negative_waste_is_rejected()
    {
        var act = () => _engine.ResolveWaste(new WasteInput(null, null, -5m));

        act.Should().Throw<DomainException>();
    }

    // --- Boxes ---------------------------------------------------------------------------

    [Fact]
    public void An_exact_box_multiple_does_not_round_up_an_extra_box()
    {
        var tile = Tile(sqFtPerBox: 10m);

        var quantity = _engine.CalculateTileQuantity(200m, tile);

        quantity.BoxesCalculated.Should().Be(20m);
        quantity.BoxesToPurchase.Should().Be(20);
    }

    [Fact]
    public void A_fraction_over_a_box_still_costs_a_whole_box()
    {
        var quantity = _engine.CalculateTileQuantity(200.01m, Tile(sqFtPerBox: 10m));

        quantity.BoxesToPurchase.Should().Be(21);
    }

    [Fact]
    public void Tile_count_uses_the_coverage_of_a_single_tile()
    {
        // A 12x12 tile covers 1 SF, so 201.6 SF needs 202 tiles.
        var quantity = _engine.CalculateTileQuantity(201.6m, Tile(coveragePerTile: 1m));

        quantity.TilesCalculated.Should().Be(201.6m);
        quantity.TilesToPurchase.Should().Be(202);
    }

    [Fact]
    public void A_tile_without_box_coverage_is_rejected_rather_than_guessed()
    {
        var tile = new TileInput(Guid.NewGuid(), "Broken", 1m, 0m, 1m, 2m);

        var act = () => _engine.CalculateTileQuantity(100m, tile);

        act.Should().Throw<DomainException>();
    }

    // --- Assembly expansion --------------------------------------------------------------

    [Fact]
    public void Thinset_quantity_comes_from_the_catalog_coverage()
    {
        // 201.6 SF at 95 SF per bag = 2.1221 bags, so 3 bags get bought.
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Thinset mortar",
            QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null, 95m, null, 18m, 28m);

        var surface = Floor(12m, 15m, waste: 12m, tile: Tile()) with { AssemblyItems = [item] };

        var result = _engine.CalculateSurface(surface);
        var thinset = result.Lines.Single(l => l.Description == "Thinset mortar");

        thinset.Quantity.Should().Be(2.1221m);
        thinset.PurchaseQuantity.Should().Be(3m);
        thinset.Unit.Should().Be(UnitOfMeasure.Bag);
        thinset.TotalCost.Should().Be(54m);  // 3 bags x $18
        thinset.TotalPrice.Should().Be(84m); // 3 bags x $28
    }

    [Fact]
    public void Backer_board_sheets_round_up_to_whole_sheets()
    {
        // 180 SF at 15 SF per sheet = 12 sheets exactly.
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Backer board",
            QuantityMethod.PerCoverage, UnitOfMeasure.Sheet, 1m, null, 15m, null, 12m, 20m);

        var surface = Floor(12m, 15m, waste: 0m) with { AssemblyItems = [item] };

        var line = _engine.CalculateSurface(surface).Lines.Single();

        line.Quantity.Should().Be(12m);
        line.PurchaseQuantity.Should().Be(12m);
    }

    [Fact]
    public void Waterproofing_supports_a_multiplier_for_a_second_coat()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Waterproofing membrane",
            QuantityMethod.PerCoverage, UnitOfMeasure.Gallon, Factor: 2m, null, Coverage: 50m,
            null, 40m, 60m);

        var surface = Floor(10m, 10m, waste: 0m) with { AssemblyItems = [item] };

        var line = _engine.CalculateSurface(surface).Lines.Single();

        line.Quantity.Should().Be(4m); // 100 / 50 x 2 coats
    }

    [Fact]
    public void Trim_is_derived_from_linear_feet_not_area()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Bullnose trim",
            QuantityMethod.PerLinearFoot, UnitOfMeasure.LinearFeet, 1m, null, null, null, 4m, 7m);

        var surface = Floor(10m, 10m, waste: 25m) with { AssemblyItems = [item], TrimLinearFeet = 32m };

        var line = _engine.CalculateSurface(surface).Lines.Single();

        line.Quantity.Should().Be(32m);
        line.Unit.Should().Be(UnitOfMeasure.LinearFeet);
    }

    [Fact]
    public void Per_each_items_use_their_fixed_quantity()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Shower drain",
            QuantityMethod.PerEach, UnitOfMeasure.Each, 1m, FixedQuantity: 1m, null, null, 85m, 140m);

        var surface = Floor(4m, 4m, waste: 0m) with { AssemblyItems = [item] };

        _engine.CalculateSurface(surface).Lines.Single().Quantity.Should().Be(1m);
    }

    [Fact]
    public void Coverage_missing_from_a_per_coverage_item_is_an_error_not_a_zero()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Mystery material",
            QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null, Coverage: null, null, 10m, 15m);

        var surface = Floor(10m, 10m) with { AssemblyItems = [item] };

        var act = () => _engine.CalculateSurface(surface);

        act.Should().Throw<DomainException>().WithMessage("*coverage*");
    }

    // --- Labor ---------------------------------------------------------------------------

    [Fact]
    public void Labor_by_unit_rate_is_quantity_times_rate()
    {
        // The SPEC 13 worked example: 450 SF x $8.00/SF = $3,600.00
        var item = new AssemblyItemInput(
            Guid.NewGuid(), null, Guid.NewGuid(), false, "Tile installation",
            QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null, null, null,
            UnitCost: 5m, UnitPrice: 8m, LaborMethod: LaborCalculationMethod.UnitRate);

        var surface = Floor(45m, 10m, waste: 0m) with { AssemblyItems = [item] };

        var line = _engine.CalculateSurface(surface).Lines.Single();

        line.Category.Should().Be(EstimateLineCategory.Labor);
        line.Quantity.Should().Be(450m);
        line.TotalPrice.Should().Be(3600m);
        line.Breakdown.Formula.Should().Contain("450 SF");
    }

    [Fact]
    public void Labor_by_productivity_converts_the_quantity_into_hours()
    {
        // 450 SF / 50 SF per hour = 9 hours; 9 x $65/hr = $585.
        var item = new AssemblyItemInput(
            Guid.NewGuid(), null, Guid.NewGuid(), false, "Tile installation",
            QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null, null, null,
            UnitCost: 45m, UnitPrice: 65m,
            LaborMethod: LaborCalculationMethod.Productivity, LaborProductivity: 50m);

        var surface = Floor(45m, 10m, waste: 0m) with { AssemblyItems = [item] };

        var line = _engine.CalculateSurface(surface).Lines.Single();

        line.Quantity.Should().Be(9m);
        line.Unit.Should().Be(UnitOfMeasure.Hour);
        line.TotalPrice.Should().Be(585m);
    }

    [Fact]
    public void Labor_lines_carry_no_waste_because_labor_is_not_a_material()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), null, Guid.NewGuid(), false, "Grouting",
            QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null, null, null, 1m, 2m,
            LaborMethod: LaborCalculationMethod.UnitRate);

        var surface = Floor(10m, 10m, waste: 15m) with { AssemblyItems = [item] };

        _engine.CalculateSurface(surface).Lines.Single().WastePercentage.Should().Be(0m);
    }

    // --- Whole takeoffs -------------------------------------------------------------------

    [Fact]
    public void A_multi_surface_takeoff_sums_its_areas()
    {
        var floor = Floor(10m, 12m, waste: 10m, tile: Tile());
        var wall = new SurfaceTakeoffInput
        {
            Name = "Wall",
            SurfaceType = SurfaceType.Wall,
            LengthFeet = 10m,
            HeightFeet = 8m,
            Waste = new WasteInput(null, null, 10m),
            Tile = Tile()
        };

        var result = _engine.Calculate([floor, wall]);

        result.TotalNetAreaSquareFeet.Should().Be(200m);      // 120 + 80
        result.TotalAdjustedAreaSquareFeet.Should().Be(220m); // both at 10% waste
        result.Surfaces.Should().HaveCount(2);
        result.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void The_surface_tile_is_not_counted_twice_when_the_assembly_also_lists_it()
    {
        var tileItem = new AssemblyItemInput(
            Guid.NewGuid(), null, null, UsesSurfaceTile: true, "Tile",
            QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null, null, null, 0m, 0m);

        var surface = Floor(10m, 10m, waste: 0m, tile: Tile()) with { AssemblyItems = [tileItem] };

        var lines = _engine.CalculateSurface(surface).Lines;

        lines.Should().ContainSingle().Which.Category.Should().Be(EstimateLineCategory.Tile);
    }

    [Fact]
    public void Every_line_explains_how_it_got_its_number()
    {
        var item = new AssemblyItemInput(
            Guid.NewGuid(), Guid.NewGuid(), null, false, "Grout",
            QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null, 120m, null, 15m, 24m);

        var surface = Floor(12m, 15m, waste: 12m, tile: Tile()) with { AssemblyItems = [item] };

        var result = _engine.CalculateSurface(surface);

        result.Lines.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Breakdown.Formula));
        result.Area.Breakdown.Formula.Should().Contain("201.6");
    }

    [Fact]
    public void The_engine_is_deterministic_for_the_same_input()
    {
        var surface = Floor(13.75m, 9.5m, waste: 17.5m, tile: Tile(sqFtPerBox: 12.35m));

        var first = _engine.CalculateSurface(surface);
        var second = _engine.CalculateSurface(surface);

        second.Area.AdjustedAreaSquareFeet.Should().Be(first.Area.AdjustedAreaSquareFeet);
        second.Lines[0].PurchaseQuantity.Should().Be(first.Lines[0].PurchaseQuantity);
    }

    [Theory]
    [InlineData(0.9999, 1)]
    [InlineData(1.0, 1)]
    [InlineData(1.0001, 2)]
    [InlineData(0, 0)]
    public void Purchase_quantities_always_round_up(decimal calculated, int expected) =>
        Rounding.PurchaseUnits(calculated).Should().Be(expected);
}
