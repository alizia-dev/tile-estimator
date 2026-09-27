using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services;
using TileEstimator.Contracts.Common;
using TileEstimator.Contracts.Projects;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Api.Controllers;

/// <summary>SPEC 9 projects, rooms, surfaces and openings.</summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public sealed class ProjectsController(
    IApplicationDbContext db,
    INumberSequenceService numbers,
    ICurrentOrganizationService currentOrganization,
    IAuditService audit)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ProjectRead)]
    [ProducesResponseType(typeof(PagedResult<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectResponse>>> List(
        [FromQuery] ProjectQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var projects = db.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            projects = projects.Where(p => p.Name.Contains(term) || p.ProjectNumber.Contains(term));
        }

        if (Enum.TryParse<ProjectStatus>(query.Status, true, out var status))
        {
            projects = projects.Where(p => p.Status == status);
        }

        if (query.CustomerId.HasValue)
        {
            projects = projects.Where(p => p.CustomerId == query.CustomerId.Value);
        }

        var total = await projects.CountAsync(ct);

        // Projected in a single query: customer name, room count and the latest estimate all
        // come back together rather than as a query per row.
        var rows = await projects
            .OrderByDescending(p => p.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(p => new
            {
                Project = p,
                CustomerName = p.Customer!.CompanyName ?? (p.Customer.FirstName + " " + p.Customer.LastName),
                RoomCount = p.Rooms.Count,
                LatestEstimate = db.Estimates
                    .Where(e => e.ProjectId == p.Id)
                    .OrderByDescending(e => e.CreatedAt)
                    .Select(e => new { e.GrandTotal, e.EstimateNumber, e.Version })
                    .FirstOrDefault(),
                LatestQuoteStatus = db.Quotes
                    .Where(q => q.ProjectId == p.Id)
                    .OrderByDescending(q => q.CreatedAt)
                    .Select(q => (QuoteStatus?)q.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => r.Project.ToResponse(
            r.CustomerName.Trim(),
            r.RoomCount,
            r.LatestEstimate?.GrandTotal,
            r.LatestEstimate is null ? null : $"{r.LatestEstimate.EstimateNumber} v{r.LatestEstimate.Version}",
            r.LatestQuoteStatus?.ToString())).ToList();

        return Ok(new PagedResult<ProjectResponse>(items, query.Page, query.PageSize, total));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.ProjectRead)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
                          .Include(p => p.Customer)
                          .FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Project), id);

        var roomCount = await db.Rooms.CountAsync(r => r.ProjectId == id, ct);

        var latestEstimate = await db.Estimates
            .Where(e => e.ProjectId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.GrandTotal, e.EstimateNumber, e.Version })
            .FirstOrDefaultAsync(ct);

        var latestQuoteStatus = await db.Quotes
            .Where(q => q.ProjectId == id)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => (QuoteStatus?)q.Status)
            .FirstOrDefaultAsync(ct);

        return Ok(project.ToResponse(
            project.Customer?.DisplayName ?? "-",
            roomCount,
            latestEstimate?.GrandTotal,
            latestEstimate is null ? null : $"{latestEstimate.EstimateNumber} v{latestEstimate.Version}",
            latestQuoteStatus?.ToString()));
    }

    [HttpPost]
    [HasPermission(Permissions.ProjectCreate)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectResponse>> Create(SaveProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        // Resolving the customer through the tenant-filtered set is what stops a project being
        // attached to another organization's customer.
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                       ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var settings = await db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        var number = await numbers.NextAsync(organizationId, "Project",
            settings?.ProjectNumberPrefix ?? "PRJ", ct);

        var project = Project.Create(organizationId, customer.Id, number, request.Name,
            CustomersController.ParseEnum<ProjectType>(request.Type, nameof(request.Type)),
            request.Description, request.SiteAddress.ToDomain());

        project.Update(request.Name, request.Description,
            CustomersController.ParseEnum<ProjectType>(request.Type, nameof(request.Type)),
            request.SiteAddress.ToDomain(), request.StartDate, request.EstimatedCompletionDate);

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Project),
            project.Id.ToString(), $"Project {project.ProjectNumber} created.", ct);

        return CreatedAtAction(nameof(Get), new { id = project.Id },
            project.ToResponse(customer.DisplayName));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Update(Guid id, SaveProjectRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await db.Projects.Include(p => p.Customer)
                          .FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Project), id);

        project.Update(request.Name, request.Description,
            CustomersController.ParseEnum<ProjectType>(request.Type, nameof(request.Type)),
            request.SiteAddress.ToDomain(), request.StartDate, request.EstimatedCompletionDate);

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(project.OrganizationId, AuditAction.Update, nameof(Project),
            project.Id.ToString(), $"Project {project.ProjectNumber} updated.", ct);

        return Ok(project.ToResponse(project.Customer?.DisplayName ?? "-"));
    }

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> UpdateStatus(Guid id,
        UpdateProjectStatusRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await db.Projects.Include(p => p.Customer)
                          .FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Project), id);

        var previous = project.Status;
        project.ChangeStatus(
            CustomersController.ParseEnum<ProjectStatus>(request.Status, nameof(request.Status)));

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(project.OrganizationId, AuditAction.Update, nameof(Project),
            project.Id.ToString(), $"Status changed from {previous} to {project.Status}.", ct);

        return Ok(project.ToResponse(project.Customer?.DisplayName ?? "-"));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ProjectDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Project), id);

        // Financial records are never removed, so a project that has quotes stays put.
        if (await db.Quotes.AnyAsync(q => q.ProjectId == id, ct))
        {
            throw new ConflictException(
                "This project has quotes and cannot be deleted. Cancel it instead.");
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(project.OrganizationId, AuditAction.Delete, nameof(Project),
            id.ToString(), $"Project {project.ProjectNumber} deleted.", ct);

        return NoContent();
    }

    // --- Rooms ---------------------------------------------------------------------------------

    [HttpGet("{projectId:guid}/rooms")]
    [HasPermission(Permissions.ProjectRead)]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomResponse>>> ListRooms(Guid projectId, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.Surfaces).ThenInclude(s => s.Openings)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var names = await LoadCatalogNamesAsync(ct);
        return Ok(rooms.Select(r => r.ToResponse(names)).ToList());
    }

    [HttpPost("{projectId:guid}/rooms")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RoomResponse>> CreateRoom(Guid projectId,
        SaveRoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException(nameof(Project), projectId);

        var room = Room.Create(project.OrganizationId, project.Id, request.Name,
            CustomersController.ParseEnum<RoomType>(request.Type, nameof(request.Type)),
            request.CustomTypeName, request.SortOrder);

        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListRooms), new { projectId }, room.ToResponse());
    }

    [HttpPut("{projectId:guid}/rooms/{roomId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoomResponse>> UpdateRoom(Guid projectId, Guid roomId,
        SaveRoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var room = await db.Rooms.Include(r => r.Surfaces).ThenInclude(s => s.Openings)
                       .FirstOrDefaultAsync(r => r.Id == roomId && r.ProjectId == projectId, ct)
                   ?? throw new NotFoundException(nameof(Room), roomId);

        room.Update(request.Name,
            CustomersController.ParseEnum<RoomType>(request.Type, nameof(request.Type)),
            request.CustomTypeName, request.Notes, request.SortOrder);

        await db.SaveChangesAsync(ct);
        return Ok(room.ToResponse(await LoadCatalogNamesAsync(ct)));
    }

    [HttpDelete("{projectId:guid}/rooms/{roomId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRoom(Guid projectId, Guid roomId, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.ProjectId == projectId, ct)
                   ?? throw new NotFoundException(nameof(Room), roomId);

        db.Rooms.Remove(room);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Surfaces -------------------------------------------------------------------------------

    [HttpPost("{projectId:guid}/rooms/{roomId:guid}/surfaces")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(SurfaceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<SurfaceResponse>> CreateSurface(Guid projectId, Guid roomId,
        SaveSurfaceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.ProjectId == projectId, ct)
                   ?? throw new NotFoundException(nameof(Room), roomId);

        await ValidateSelectionsAsync(request, ct);

        var surface = Surface.Create(room.OrganizationId, room.Id, request.Name,
            CustomersController.ParseEnum<SurfaceType>(request.Type, nameof(request.Type)),
            request.LengthFeet, request.WidthFeet, request.HeightFeet, request.SortOrder);

        surface.UpdateDimensions(request.LengthFeet, request.WidthFeet, request.HeightFeet,
            request.AreaOverrideSquareFeet, request.TrimLinearFeet);
        surface.UpdateSelections(request.TileId, request.PatternId, request.AssemblyId,
            request.WasteOverridePercentage);
        surface.UpdateDetails(request.Name, request.Notes, request.SortOrder);

        db.Surfaces.Add(surface);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListRooms), new { projectId },
            surface.ToResponse(await LoadCatalogNamesAsync(ct)));
    }

    [HttpPut("{projectId:guid}/rooms/{roomId:guid}/surfaces/{surfaceId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(SurfaceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SurfaceResponse>> UpdateSurface(Guid projectId, Guid roomId,
        Guid surfaceId, SaveSurfaceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var surface = await db.Surfaces.Include(s => s.Openings)
                          .FirstOrDefaultAsync(s => s.Id == surfaceId && s.RoomId == roomId, ct)
                      ?? throw new NotFoundException(nameof(Surface), surfaceId);

        await ValidateSelectionsAsync(request, ct);

        surface.UpdateDimensions(request.LengthFeet, request.WidthFeet, request.HeightFeet,
            request.AreaOverrideSquareFeet, request.TrimLinearFeet);
        surface.UpdateSelections(request.TileId, request.PatternId, request.AssemblyId,
            request.WasteOverridePercentage);
        surface.UpdateDetails(request.Name, request.Notes, request.SortOrder);

        await db.SaveChangesAsync(ct);
        return Ok(surface.ToResponse(await LoadCatalogNamesAsync(ct)));
    }

    [HttpDelete("{projectId:guid}/rooms/{roomId:guid}/surfaces/{surfaceId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSurface(Guid projectId, Guid roomId, Guid surfaceId,
        CancellationToken ct)
    {
        var surface = await db.Surfaces.FirstOrDefaultAsync(s => s.Id == surfaceId && s.RoomId == roomId, ct)
                      ?? throw new NotFoundException(nameof(Surface), surfaceId);

        db.Surfaces.Remove(surface);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Openings --------------------------------------------------------------------------------

    [HttpPost("{projectId:guid}/surfaces/{surfaceId:guid}/openings")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(OpeningResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OpeningResponse>> CreateOpening(Guid projectId, Guid surfaceId,
        SaveOpeningRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var surface = await db.Surfaces.FirstOrDefaultAsync(s => s.Id == surfaceId, ct)
                      ?? throw new NotFoundException(nameof(Surface), surfaceId);

        var type = CustomersController.ParseEnum<OpeningType>(request.Type, nameof(request.Type));

        var opening = Opening.Create(surface.OrganizationId, surface.Id, request.Name, type,
            request.WidthFeet, request.HeightFeet, request.Quantity);

        if (request.AddsArea.HasValue)
        {
            opening.Update(request.Name, type, request.WidthFeet, request.HeightFeet,
                request.Quantity, request.AddsArea.Value);
        }

        db.Openings.Add(opening);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListRooms), new { projectId }, opening.ToResponse());
    }

    [HttpPut("{projectId:guid}/surfaces/{surfaceId:guid}/openings/{openingId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(typeof(OpeningResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpeningResponse>> UpdateOpening(Guid projectId, Guid surfaceId,
        Guid openingId, SaveOpeningRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var opening = await db.Openings
                          .FirstOrDefaultAsync(o => o.Id == openingId && o.SurfaceId == surfaceId, ct)
                      ?? throw new NotFoundException(nameof(Opening), openingId);

        var type = CustomersController.ParseEnum<OpeningType>(request.Type, nameof(request.Type));

        opening.Update(request.Name, type, request.WidthFeet, request.HeightFeet, request.Quantity,
            request.AddsArea ?? type is OpeningType.Niche or OpeningType.Bench);

        await db.SaveChangesAsync(ct);
        return Ok(opening.ToResponse());
    }

    [HttpDelete("{projectId:guid}/surfaces/{surfaceId:guid}/openings/{openingId:guid}")]
    [HasPermission(Permissions.ProjectUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteOpening(Guid projectId, Guid surfaceId, Guid openingId,
        CancellationToken ct)
    {
        var opening = await db.Openings
                          .FirstOrDefaultAsync(o => o.Id == openingId && o.SurfaceId == surfaceId, ct)
                      ?? throw new NotFoundException(nameof(Opening), openingId);

        db.Openings.Remove(opening);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Notes -----------------------------------------------------------------------------------

    [HttpGet("{projectId:guid}/notes")]
    [HasPermission(Permissions.ProjectRead)]
    public async Task<ActionResult<IReadOnlyList<ProjectNoteResponse>>> ListNotes(Guid projectId,
        CancellationToken ct)
    {
        var notes = await db.ProjectNotes.AsNoTracking()
            .Where(n => n.ProjectId == projectId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ProjectNoteResponse(n.Id, n.Body, n.CreatedAt, n.CreatedBy))
            .ToListAsync(ct);

        return Ok(notes);
    }

    [HttpPost("{projectId:guid}/notes")]
    [HasPermission(Permissions.ProjectUpdate)]
    public async Task<ActionResult<ProjectNoteResponse>> CreateNote(Guid projectId,
        SaveProjectNoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException(nameof(Project), projectId);

        var note = ProjectNote.Create(project.OrganizationId, project.Id, request.Body);
        db.ProjectNotes.Add(note);
        await db.SaveChangesAsync(ct);

        return Ok(new ProjectNoteResponse(note.Id, note.Body, note.CreatedAt, note.CreatedBy));
    }

    /// <summary>
    /// Rejects a tile, pattern or assembly the caller cannot see. Because the sets are already
    /// tenant-filtered, an id from another organization simply does not exist here.
    /// </summary>
    private async Task ValidateSelectionsAsync(SaveSurfaceRequest request, CancellationToken ct)
    {
        if (request.TileId.HasValue && !await db.Tiles.AnyAsync(t => t.Id == request.TileId.Value, ct))
        {
            throw new ValidationFailedException(nameof(request.TileId), "That tile was not found in your catalog.");
        }

        if (request.PatternId.HasValue && !await db.Patterns.AnyAsync(p => p.Id == request.PatternId.Value, ct))
        {
            throw new ValidationFailedException(nameof(request.PatternId), "That pattern was not found.");
        }

        if (request.AssemblyId.HasValue && !await db.Assemblies.AnyAsync(a => a.Id == request.AssemblyId.Value, ct))
        {
            throw new ValidationFailedException(nameof(request.AssemblyId), "That assembly was not found.");
        }
    }

    private async Task<CatalogNameLookup> LoadCatalogNamesAsync(CancellationToken ct) => new(
        await db.Tiles.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.ProductName, ct),
        await db.Patterns.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct),
        await db.Assemblies.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, ct));
}
