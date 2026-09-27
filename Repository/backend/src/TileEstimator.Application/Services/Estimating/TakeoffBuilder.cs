using Microsoft.EntityFrameworkCore;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Application.Services.Estimating;

/// <summary>
/// Turns persisted rooms and surfaces into the engine's input records: it resolves the tile,
/// the pattern default, the best-matching waste rule and the assembly items from the catalog.
/// <para>
/// This class does no arithmetic. Every number still comes out of <see cref="ITakeoffEngine"/>,
/// which is what keeps the quick calculators and the project takeoff in agreement.
/// </para>
/// </summary>
public sealed class TakeoffBuilder(IApplicationDbContext db)
{
    /// <summary>Builds engine inputs for every surface in a project.</summary>
    public async Task<List<SurfaceTakeoffInput>> BuildForProjectAsync(Guid projectId,
        CancellationToken cancellationToken)
    {
        var rooms = await db.Rooms
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.Surfaces).ThenInclude(s => s.Openings)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        if (rooms.Count == 0)
        {
            throw new ValidationFailedException("rooms",
                "This project has no rooms yet. Add a room and its surfaces before calculating.");
        }

        var catalog = await LoadCatalogAsync(cancellationToken);
        var inputs = new List<SurfaceTakeoffInput>();

        foreach (var room in rooms)
        {
            foreach (var surface in room.Surfaces.OrderBy(s => s.SortOrder))
            {
                inputs.Add(BuildSurfaceInput(surface, room, catalog));
            }
        }

        if (inputs.Count == 0)
        {
            throw new ValidationFailedException("surfaces",
                "No surfaces have been defined for this project's rooms.");
        }

        return inputs;
    }

    /// <summary>Builds the engine input for a single persisted surface.</summary>
    public async Task<SurfaceTakeoffInput> BuildForSurfaceAsync(Guid surfaceId,
        CancellationToken cancellationToken)
    {
        var surface = await db.Surfaces
            .Include(s => s.Openings)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == surfaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Surface), surfaceId);

        var catalog = await LoadCatalogAsync(cancellationToken);
        return BuildSurfaceInput(surface, surface.Room, catalog);
    }

    /// <summary>The catalog rows a takeoff needs, loaded once per calculation to avoid N+1 queries.</summary>
    public async Task<CatalogSnapshot> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var tiles = await db.Tiles.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        var patterns = await db.Patterns.AsNoTracking().ToDictionaryAsync(p => p.Id, cancellationToken);
        var materials = await db.Materials.AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
        var laborRates = await db.LaborRates.AsNoTracking().ToDictionaryAsync(l => l.Id, cancellationToken);

        var wasteRules = await db.WasteRules.AsNoTracking()
            .Where(w => w.Active)
            .ToListAsync(cancellationToken);

        var assemblies = await db.Assemblies.AsNoTracking()
            .Include(a => a.Items)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        return new CatalogSnapshot(tiles, patterns, materials, laborRates, wasteRules, assemblies);
    }

    /// <summary>Assembles one surface's engine input from its row and the catalog.</summary>
    public SurfaceTakeoffInput BuildSurfaceInput(Surface surface, Room? room, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(catalog);

        var tile = surface.TileId.HasValue && catalog.Tiles.TryGetValue(surface.TileId.Value, out var t)
            ? new TileInput(t.Id, $"{t.ProductName} ({t.Sku})", t.CoverageSqFt, t.SqFtPerBox,
                t.CostPerSqFt, t.SellingPricePerSqFt)
            : null;

        var pattern = surface.PatternId.HasValue && catalog.Patterns.TryGetValue(surface.PatternId.Value, out var p)
            ? p
            : null;

        var matchedRule = FindBestWasteRule(catalog.WasteRules, surface, room, tile is null ? null :
            catalog.Tiles[surface.TileId!.Value].MaterialType);

        var waste = new WasteInput(
            pattern?.DefaultWastePercentage,
            matchedRule?.WastePercentage,
            surface.WasteOverridePercentage,
            pattern?.Name,
            matchedRule?.Name);

        var assemblyItems = new List<AssemblyItemInput>();

        if (surface.AssemblyId.HasValue && catalog.Assemblies.TryGetValue(surface.AssemblyId.Value, out var assembly))
        {
            foreach (var item in assembly.Items.OrderBy(i => i.SortOrder))
            {
                var input = BuildAssemblyItemInput(item, catalog);
                if (input is not null)
                {
                    assemblyItems.Add(input);
                }
            }
        }

        return new SurfaceTakeoffInput
        {
            SurfaceId = surface.Id,
            RoomId = surface.RoomId,
            Name = surface.Name,
            RoomName = room?.Name,
            SurfaceType = surface.Type,
            LengthFeet = surface.LengthFeet,
            WidthFeet = surface.WidthFeet,
            HeightFeet = surface.HeightFeet,
            AreaOverrideSquareFeet = surface.AreaOverrideSquareFeet,
            TrimLinearFeet = surface.TrimLinearFeet,
            AssemblyId = surface.AssemblyId,
            Tile = tile,
            Waste = waste,
            AssemblyItems = assemblyItems,
            Openings = surface.Openings
                .Select(o => new OpeningInput(o.Name, o.Type, o.WidthFeet, o.HeightFeet, o.Quantity, o.AddsArea))
                .ToList()
        };
    }

    private static AssemblyItemInput? BuildAssemblyItemInput(
        Domain.Estimation.AssemblyItem item, CatalogSnapshot catalog)
    {
        if (item.UsesSurfaceTile)
        {
            return new AssemblyItemInput(item.Id, null, null, true, "Tile",
                item.QuantityMethod, item.Unit ?? Domain.ValueObjects.UnitOfMeasure.SquareFeet,
                item.Factor, item.FixedQuantity, item.CoverageOverride, item.WasteOverridePercentage,
                0m, 0m, SortOrder: item.SortOrder);
        }

        if (item.MaterialId.HasValue)
        {
            if (!catalog.Materials.TryGetValue(item.MaterialId.Value, out var material) || !material.Active)
            {
                // A deleted or deactivated material drops out of new calculations. Existing
                // estimate lines are untouched, because they hold their own snapshot.
                return null;
            }

            return new AssemblyItemInput(
                item.Id, material.Id, null, false, material.Name,
                item.QuantityMethod, item.Unit ?? material.Unit, item.Factor, item.FixedQuantity,
                // The assembly's coverage override wins; otherwise the catalog value is used.
                item.CoverageOverride ?? material.Coverage,
                item.WasteOverridePercentage,
                material.Cost, material.SellingPrice,
                SortOrder: item.SortOrder);
        }

        if (item.LaborRateId.HasValue)
        {
            if (!catalog.LaborRates.TryGetValue(item.LaborRateId.Value, out var labor) || !labor.Active)
            {
                return null;
            }

            // Labor "cost" and "price" are both the configured rate; the pricing engine adds the
            // margin later, so no markup is baked into the line here.
            return new AssemblyItemInput(
                item.Id, null, labor.Id, false, labor.Name,
                item.QuantityMethod, item.Unit ?? labor.Unit, item.Factor, item.FixedQuantity,
                item.CoverageOverride, item.WasteOverridePercentage,
                labor.Rate, labor.Rate,
                labor.CalculationMethod, labor.Productivity,
                item.SortOrder);
        }

        return null;
    }

    /// <summary>
    /// SPEC 11 rule matching: a null field on a rule means "any". The most specific active
    /// match wins, with <c>Priority</c> breaking ties.
    /// </summary>
    private static Domain.Estimation.WasteRule? FindBestWasteRule(
        IReadOnlyList<Domain.Estimation.WasteRule> rules,
        Surface surface,
        Room? room,
        TileMaterialType? tileMaterialType) =>
        rules
            .Where(r =>
                (r.PatternId is null || r.PatternId == surface.PatternId) &&
                (r.SurfaceType is null || r.SurfaceType == surface.Type) &&
                (r.TileMaterialType is null || r.TileMaterialType == tileMaterialType) &&
                (r.RoomType is null || r.RoomType == room?.Type))
            .OrderByDescending(r => r.Specificity)
            .ThenByDescending(r => r.Priority)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .FirstOrDefault();
}

/// <summary>The catalog rows one calculation needs, read once up front.</summary>
public sealed record CatalogSnapshot(
    IReadOnlyDictionary<Guid, Domain.Catalog.Tile> Tiles,
    IReadOnlyDictionary<Guid, Domain.Estimation.Pattern> Patterns,
    IReadOnlyDictionary<Guid, Domain.Catalog.Material> Materials,
    IReadOnlyDictionary<Guid, Domain.Estimation.LaborRate> LaborRates,
    IReadOnlyList<Domain.Estimation.WasteRule> WasteRules,
    IReadOnlyDictionary<Guid, Domain.Estimation.Assembly> Assemblies);
