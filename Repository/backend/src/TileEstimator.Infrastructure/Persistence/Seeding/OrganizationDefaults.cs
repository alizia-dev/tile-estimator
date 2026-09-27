using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Infrastructure.Persistence.Seeding;

/// <summary>
/// The default estimation configuration created for every new organization at registration
/// (SPEC 5). These are starting points the contractor owns and edits, not system constants:
/// every waste percentage, coverage and rate below is an ordinary editable row afterwards.
/// </summary>
public static class OrganizationDefaults
{
    /// <summary>
    /// SPEC 11 patterns with a starting waste percentage each. A diagonal or herringbone layout
    /// produces more offcuts than a straight lay, which is why the defaults differ.
    /// </summary>
    public static IReadOnlyList<Pattern> CreatePatterns(Guid organizationId) =>
    [
        Pattern.Create(organizationId, "Straight Lay", "Tiles aligned in a simple grid", 10m, 1),
        Pattern.Create(organizationId, "Grid", "Square grid layout", 10m, 2),
        Pattern.Create(organizationId, "Running Bond", "Each row offset by half a tile", 12m, 3),
        Pattern.Create(organizationId, "Brick", "Brick-style offset layout", 12m, 4),
        Pattern.Create(organizationId, "Diagonal", "Rotated 45 degrees; more cuts at the edges", 15m, 5),
        Pattern.Create(organizationId, "Herringbone", "Interlocking V pattern; the most cutting", 20m, 6),
        Pattern.Create(organizationId, "Stacked", "Tiles stacked in aligned columns", 10m, 7),
        Pattern.Create(organizationId, "Custom", "Define your own layout and waste", 10m, 8)
    ];

    /// <summary>
    /// Starting waste rules. Each is more specific than a bare pattern default, and the engine
    /// prefers the most specific active match.
    /// </summary>
    public static IReadOnlyList<WasteRule> CreateWasteRules(Guid organizationId) =>
    [
        WasteRule.Create(organizationId, "Natural stone (extra breakage)", 15m, 20,
            null, null, TileMaterialType.NaturalStone, null),
        WasteRule.Create(organizationId, "Marble (extra breakage)", 15m, 20,
            null, null, TileMaterialType.Marble, null),
        WasteRule.Create(organizationId, "Mosaic sheets", 12m, 15,
            null, null, TileMaterialType.Mosaic, null),
        WasteRule.Create(organizationId, "Shower walls (many cuts)", 15m, 30,
            null, SurfaceType.ShowerWall, null, null),
        WasteRule.Create(organizationId, "Shower floors (slope and drain cuts)", 20m, 30,
            null, SurfaceType.ShowerFloor, null, null),
        WasteRule.Create(organizationId, "Backsplash (outlets and edges)", 15m, 25,
            null, SurfaceType.Backsplash, null, null)
    ];

    /// <summary>SPEC 13 starting labor rates, covering both calculation methods.</summary>
    public static IReadOnlyList<LaborRate> CreateLaborRates(Guid organizationId) =>
    [
        LaborRate.Create(organizationId, "Tile Installation - Floor", "Tile Setting",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 8m, null),
        LaborRate.Create(organizationId, "Tile Installation - Wall", "Tile Setting",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 10m, null),
        LaborRate.Create(organizationId, "Tile Installation - Shower", "Tile Setting",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 14m, null),
        LaborRate.Create(organizationId, "Backer Board Installation", "Substrate",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 3.50m, null),
        LaborRate.Create(organizationId, "Waterproofing", "Substrate",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 4m, null),
        LaborRate.Create(organizationId, "Demolition", "Prep",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.Productivity, 65m, 40m),
        LaborRate.Create(organizationId, "Grouting", "Finishing",
            UnitOfMeasure.SquareFeet, LaborCalculationMethod.UnitRate, 2m, null),
        LaborRate.Create(organizationId, "Trim Installation", "Finishing",
            UnitOfMeasure.LinearFeet, LaborCalculationMethod.UnitRate, 6m, null)
    ];

    /// <summary>
    /// Starting materials. Coverage is the number the takeoff engine divides area by, so each
    /// one is set explicitly rather than assumed anywhere in the engine.
    /// </summary>
    public static IReadOnlyList<Material> CreateMaterials(Guid organizationId) =>
    [
        Material.Create(organizationId, "THIN-MOD-50", "Modified Thinset Mortar (50 lb)",
            MaterialCategory.Thinset, UnitOfMeasure.Bag, 95m, 18m, 28m),
        Material.Create(organizationId, "THIN-LHT-50", "Large Format Thinset (50 lb)",
            MaterialCategory.Thinset, UnitOfMeasure.Bag, 75m, 24m, 36m),
        Material.Create(organizationId, "GROUT-SAND-25", "Sanded Grout (25 lb)",
            MaterialCategory.Grout, UnitOfMeasure.Bag, 120m, 22m, 34m),
        Material.Create(organizationId, "GROUT-UNSAND-25", "Unsanded Grout (25 lb)",
            MaterialCategory.Grout, UnitOfMeasure.Bag, 100m, 24m, 37m),
        Material.Create(organizationId, "BACKER-1/2", "Cement Backer Board 3x5 (1/2 in)",
            MaterialCategory.BackerBoard, UnitOfMeasure.Sheet, 15m, 14m, 22m),
        Material.Create(organizationId, "BACKER-1/4", "Cement Backer Board 3x5 (1/4 in)",
            MaterialCategory.BackerBoard, UnitOfMeasure.Sheet, 15m, 12m, 19m),
        Material.Create(organizationId, "WP-MEMB-GAL", "Waterproofing Membrane (1 gal)",
            MaterialCategory.WaterproofingMembrane, UnitOfMeasure.Gallon, 50m, 42m, 65m),
        Material.Create(organizationId, "SEAL-GAL", "Stone Sealer (1 gal)",
            MaterialCategory.Sealer, UnitOfMeasure.Gallon, 200m, 38m, 58m),
        Material.Create(organizationId, "CAULK-TUBE", "Color-Matched Caulk",
            MaterialCategory.Caulk, UnitOfMeasure.Tube, 25m, 9m, 15m),
        Material.Create(organizationId, "PRIMER-GAL", "Substrate Primer (1 gal)",
            MaterialCategory.Primer, UnitOfMeasure.Gallon, 250m, 32m, 48m),
        Material.Create(organizationId, "ADH-MASTIC-GAL", "Tile Mastic Adhesive (1 gal)",
            MaterialCategory.Adhesive, UnitOfMeasure.Gallon, 40m, 28m, 43m),
        Material.Create(organizationId, "TRIM-SCHLUTER-LF", "Metal Edge Trim",
            MaterialCategory.Trim, UnitOfMeasure.LinearFeet, null, 4m, 7m),
        Material.Create(organizationId, "BULLNOSE-LF", "Bullnose Trim Tile",
            MaterialCategory.Bullnose, UnitOfMeasure.LinearFeet, null, 6m, 10m),
        Material.Create(organizationId, "SPACERS-BAG", "Tile Spacers (bag)",
            MaterialCategory.Spacers, UnitOfMeasure.Bag, 200m, 6m, 10m)
    ];

    /// <summary>
    /// SPEC 12 starter assemblies. Each is a complete installation system, and applying one to a
    /// surface expands into the tile, materials and labor that job actually needs.
    /// </summary>
    public static IReadOnlyList<AssemblyDefinition> AssemblyDefinitions =>
    [
        new("Standard Floor", "Tile, thinset, grout and installation labor", SurfaceType.Floor,
        [
            new AssemblyItemDefinition(null, null, UsesSurfaceTile: true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("THIN-MOD-50", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("GROUT-SAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Floor", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Grouting", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ]),

        new("Bathroom Floor", "Standard floor plus sealer for a wet area", SurfaceType.Floor,
        [
            new AssemblyItemDefinition(null, null, true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("THIN-MOD-50", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("GROUT-SAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("SEAL-GAL", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Gallon, 1m, null),
            new AssemblyItemDefinition("BACKER-1/4", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Sheet, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Floor", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Grouting", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ]),

        new("Shower Wall", "Waterproofed shower wall system", SurfaceType.ShowerWall,
        [
            new AssemblyItemDefinition(null, null, true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("BACKER-1/2", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Sheet, 1m, null),
            // Two coats of membrane, which is why the factor is 2.
            new AssemblyItemDefinition("WP-MEMB-GAL", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Gallon, 2m, null),
            new AssemblyItemDefinition("THIN-MOD-50", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("GROUT-UNSAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("CAULK-TUBE", null, false, QuantityMethod.PerEach, UnitOfMeasure.Tube, 1m, 2m),
            new AssemblyItemDefinition("TRIM-SCHLUTER-LF", null, false, QuantityMethod.PerLinearFoot, UnitOfMeasure.LinearFeet, 1m, null),
            new AssemblyItemDefinition(null, "Waterproofing", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Backer Board Installation", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Shower", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Grouting", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ]),

        new("Shower Floor", "Sloped, waterproofed shower pan", SurfaceType.ShowerFloor,
        [
            new AssemblyItemDefinition(null, null, true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("WP-MEMB-GAL", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Gallon, 2m, null),
            new AssemblyItemDefinition("THIN-MOD-50", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("GROUT-UNSAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition(null, "Waterproofing", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Shower", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ]),

        new("Backsplash", "Kitchen or vanity backsplash", SurfaceType.Backsplash,
        [
            new AssemblyItemDefinition(null, null, true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("ADH-MASTIC-GAL", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Gallon, 1m, null),
            new AssemblyItemDefinition("GROUT-UNSAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("CAULK-TUBE", null, false, QuantityMethod.PerEach, UnitOfMeasure.Tube, 1m, 1m),
            new AssemblyItemDefinition("TRIM-SCHLUTER-LF", null, false, QuantityMethod.PerLinearFoot, UnitOfMeasure.LinearFeet, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Wall", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Grouting", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ]),

        new("Standard Wall", "Tiled wall with thinset and grout", SurfaceType.Wall,
        [
            new AssemblyItemDefinition(null, null, true, QuantityMethod.PerArea, null, 1m, null),
            new AssemblyItemDefinition("THIN-MOD-50", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition("GROUT-UNSAND-25", null, false, QuantityMethod.PerCoverage, UnitOfMeasure.Bag, 1m, null),
            new AssemblyItemDefinition(null, "Tile Installation - Wall", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null),
            new AssemblyItemDefinition(null, "Grouting", false, QuantityMethod.PerArea, UnitOfMeasure.SquareFeet, 1m, null)
        ])
    ];
}

/// <summary>A seed assembly, described by the SKUs and labor-rate names it pulls together.</summary>
public sealed record AssemblyDefinition(
    string Name,
    string Description,
    SurfaceType? AppliesTo,
    IReadOnlyList<AssemblyItemDefinition> Items);

/// <summary>One line of a seed assembly, referenced by SKU or labor-rate name rather than by id.</summary>
public sealed record AssemblyItemDefinition(
    string? MaterialSku,
    string? LaborRateName,
    bool UsesSurfaceTile,
    QuantityMethod QuantityMethod,
    string? Unit,
    decimal Factor,
    decimal? FixedQuantity);
