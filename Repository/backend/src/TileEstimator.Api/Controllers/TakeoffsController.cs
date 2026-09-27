using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Application.Services.Estimating;
using TileEstimator.Contracts.Estimating;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// SPEC 14 and SPEC 16. Project takeoffs and the five quick calculators.
/// <para>
/// Every endpoint here builds engine inputs and hands them to the same
/// <see cref="ITakeoffEngine"/>. No formula is implemented in this controller, which is what
/// guarantees a quick calculator and a project takeoff agree on identical inputs.
/// </para>
/// </summary>
[ApiController]
[Route("api/takeoffs")]
[Produces("application/json")]
public sealed class TakeoffsController(
    ITakeoffEngine engine,
    TakeoffBuilder builder)
    : ControllerBase
{
    /// <summary>Calculates the takeoff for every surface in a project.</summary>
    [HttpPost("calculate")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TakeoffResponse>> CalculateForProject(
        [FromQuery] Guid projectId, CancellationToken ct)
    {
        var inputs = await builder.BuildForProjectAsync(projectId, ct);
        return Ok(engine.Calculate(inputs).ToResponse());
    }

    /// <summary>Calculates the takeoff for one surface.</summary>
    [HttpPost("calculate-surface")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> CalculateForSurface(
        [FromQuery] Guid surfaceId, CancellationToken ct)
    {
        var input = await builder.BuildForSurfaceAsync(surfaceId, ct);
        return Ok(engine.Calculate([input]).ToResponse());
    }

    // --- SPEC 16 quick calculators ------------------------------------------------------------

    [HttpPost("quick/floor")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> QuickFloor(
        FloorCalculatorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(ct);
        var surface = BuildQuickSurface(context, "Floor", SurfaceType.Floor,
            request.LengthFeet, request.WidthFeet, null, request.TileId, request.PatternId,
            request.AssemblyId, request.WasteOverridePercentage, request.Openings,
            request.TrimLinearFeet);

        return Ok(engine.Calculate([surface]).ToResponse());
    }

    [HttpPost("quick/wall")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> QuickWall(
        WallCalculatorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(ct);
        var surface = BuildQuickSurface(context, "Wall", SurfaceType.Wall,
            request.LengthFeet, null, request.HeightFeet, request.TileId, request.PatternId,
            request.AssemblyId, request.WasteOverridePercentage, request.Openings, 0m);

        return Ok(engine.Calculate([surface]).ToResponse());
    }

    [HttpPost("quick/backsplash")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> QuickBacksplash(
        BacksplashCalculatorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(ct);
        var surface = BuildQuickSurface(context, "Backsplash", SurfaceType.Backsplash,
            request.LengthFeet, null, request.HeightFeet, request.TileId, request.PatternId,
            request.AssemblyId, request.WasteOverridePercentage, request.Openings,
            request.LengthFeet);

        return Ok(engine.Calculate([surface]).ToResponse());
    }

    [HttpPost("quick/shower")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> QuickShower(
        ShowerCalculatorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(ct);
        return Ok(engine.Calculate(BuildShowerSurfaces(context, request)).ToResponse());
    }

    /// <summary>
    /// The full bathroom: floor, walls, an optional shower and an optional backsplash, all run
    /// through the same engine call so the totals are consistent with the individual calculators.
    /// </summary>
    [HttpPost("quick/bathroom")]
    [HasPermission(Permissions.EstimateRead)]
    [ProducesResponseType(typeof(TakeoffResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TakeoffResponse>> QuickBathroom(
        BathroomCalculatorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(ct);
        var surfaces = new List<SurfaceTakeoffInput>
        {
            BuildQuickSurface(context, "Bathroom Floor", SurfaceType.Floor,
                request.FloorLengthFeet, request.FloorWidthFeet, null,
                request.FloorTileId, request.PatternId, request.FloorAssemblyId,
                request.WasteOverridePercentage, [], 0m)
        };

        if (request.WallLengthFeet > 0m && request.WallHeightFeet > 0m)
        {
            surfaces.Add(BuildQuickSurface(context, "Bathroom Walls", SurfaceType.Wall,
                request.WallLengthFeet, null, request.WallHeightFeet,
                request.WallTileId, request.PatternId, request.WallAssemblyId,
                request.WasteOverridePercentage, [], 0m));
        }

        if (request.Shower is not null)
        {
            surfaces.AddRange(BuildShowerSurfaces(context, request.Shower));
        }

        if (request.BacksplashLengthFeet > 0m && request.BacksplashHeightFeet > 0m)
        {
            surfaces.Add(BuildQuickSurface(context, "Vanity Backsplash", SurfaceType.Backsplash,
                request.BacksplashLengthFeet, null, request.BacksplashHeightFeet,
                request.BacksplashTileId, request.PatternId, request.BacksplashAssemblyId,
                request.WasteOverridePercentage, [], request.BacksplashLengthFeet));
        }

        return Ok(engine.Calculate(surfaces).ToResponse());
    }

    /// <summary>
    /// A shower is three surfaces: the pan, the wall run around it, and the niche or bench that
    /// adds tiled area rather than removing it.
    /// </summary>
    private static List<SurfaceTakeoffInput> BuildShowerSurfaces(
        QuickCalculationContext context, ShowerCalculatorRequest request)
    {
        // Three walls around a standard alcove: back plus two sides.
        var wallRun = request.WidthFeet + (request.DepthFeet * 2m);

        var openings = new List<QuickOpeningRequest>();

        if (request.DoorWidthFeet > 0m && request.DoorHeightFeet > 0m)
        {
            openings.Add(new QuickOpeningRequest
            {
                Name = "Shower door",
                Type = nameof(OpeningType.Door),
                WidthFeet = request.DoorWidthFeet,
                HeightFeet = request.DoorHeightFeet,
                AddsArea = false
            });
        }

        if (request.IncludeNiche)
        {
            openings.Add(new QuickOpeningRequest
            {
                Name = "Niche",
                Type = nameof(OpeningType.Niche),
                WidthFeet = request.NicheWidthFeet,
                HeightFeet = request.NicheHeightFeet,
                AddsArea = true
            });
        }

        if (request.IncludeBench)
        {
            openings.Add(new QuickOpeningRequest
            {
                Name = "Bench",
                Type = nameof(OpeningType.Bench),
                WidthFeet = request.BenchWidthFeet,
                HeightFeet = request.BenchDepthFeet,
                AddsArea = true
            });
        }

        return
        [
            BuildQuickSurface(context, "Shower Floor", SurfaceType.ShowerFloor,
                request.WidthFeet, request.DepthFeet, null, request.FloorTileId,
                request.PatternId, request.FloorAssemblyId, request.WasteOverridePercentage,
                [], 0m),

            BuildQuickSurface(context, "Shower Walls", SurfaceType.ShowerWall,
                wallRun, null, request.WallHeightFeet, request.WallTileId,
                request.PatternId, request.WallAssemblyId, request.WasteOverridePercentage,
                openings, wallRun)
        ];
    }

    private static SurfaceTakeoffInput BuildQuickSurface(
        QuickCalculationContext context,
        string name,
        SurfaceType type,
        decimal length,
        decimal? width,
        decimal? height,
        Guid? tileId,
        Guid? patternId,
        Guid? assemblyId,
        decimal? wasteOverride,
        IReadOnlyList<QuickOpeningRequest> openings,
        decimal trimLinearFeet)
    {
        // A quick calculator has no persisted Surface row, so a detached one is built and put
        // through exactly the same builder the project takeoff uses.
        var surface = Domain.Projects.Surface.Create(Guid.Empty, Guid.Empty, name, type,
            length, width, height, 0);

        surface.UpdateDimensions(length, width, height, null, trimLinearFeet);
        surface.UpdateSelections(tileId, patternId, assemblyId, wasteOverride);

        foreach (var opening in openings)
        {
            var openingType = CustomersController.ParseEnum<OpeningType>(opening.Type, nameof(opening.Type));
            var entity = Domain.Projects.Opening.Create(Guid.Empty, Guid.Empty, opening.Name,
                openingType, opening.WidthFeet, opening.HeightFeet, opening.Quantity);

            if (opening.AddsArea.HasValue)
            {
                entity.Update(opening.Name, openingType, opening.WidthFeet, opening.HeightFeet,
                    opening.Quantity, opening.AddsArea.Value);
            }

            surface.Openings.Add(entity);
        }

        var input = context.Builder.BuildSurfaceInput(surface, null, context.Catalog);

        // The detached surface has no id, so the engine result should not carry a fake one.
        return input with { SurfaceId = null, RoomId = null, Name = name };
    }

    private async Task<QuickCalculationContext> LoadContextAsync(CancellationToken ct) =>
        new(builder, await builder.LoadCatalogAsync(ct));

    private sealed record QuickCalculationContext(TakeoffBuilder Builder, CatalogSnapshot Catalog);
}
