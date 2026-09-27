namespace TileEstimator.Application.Engines.Takeoff;

/// <summary>
/// SPEC 14 takeoff engine: "how much material is required?". Pure, deterministic and synchronous.
/// Every quantity in the product comes from here, including the quick calculators, so no formula
/// is written twice.
/// </summary>
public interface ITakeoffEngine
{
    /// <summary>Resolves waste in the SPEC 11 order: pattern default, waste rule, estimator override.</summary>
    WasteResolution ResolveWaste(WasteInput input);

    /// <summary>Gross area, opening adjustments, net area and the waste-adjusted area for one surface.</summary>
    AreaResult CalculateArea(SurfaceTakeoffInput surface);

    /// <summary>Tiles and boxes for an already waste-adjusted area.</summary>
    TileQuantityResult CalculateTileQuantity(decimal adjustedAreaSquareFeet, TileInput tile);

    /// <summary>Expands one surface into tile, material and labor lines using its assembly.</summary>
    SurfaceTakeoffResult CalculateSurface(SurfaceTakeoffInput surface);

    /// <summary>Runs every surface and returns the combined takeoff.</summary>
    TakeoffResult Calculate(IReadOnlyCollection<SurfaceTakeoffInput> surfaces);
}
