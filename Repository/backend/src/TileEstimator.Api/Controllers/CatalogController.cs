using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services;
using TileEstimator.Contracts.Catalog;
using TileEstimator.Contracts.Common;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;

namespace TileEstimator.Api.Controllers;

/// <summary>SPEC 10 to SPEC 13: tiles, materials, patterns, waste rules, labor rates and assemblies.</summary>
[ApiController]
[Route("api/catalog")]
[Produces("application/json")]
public sealed class CatalogController(
    IApplicationDbContext db,
    ICurrentOrganizationService currentOrganization,
    IAuditService audit)
    : ControllerBase
{
    // --- Tiles ------------------------------------------------------------------------------

    [HttpGet("tiles")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(PagedResult<TileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TileResponse>>> ListTiles(
        [FromQuery] CatalogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = db.Tiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(t => t.Sku.Contains(term) || t.ProductName.Contains(term) ||
                                       (t.Brand != null && t.Brand.Contains(term)));
        }

        if (query.Active.HasValue)
        {
            source = source.Where(t => t.Active == query.Active.Value);
        }

        if (Enum.TryParse<TileMaterialType>(query.MaterialType, true, out var materialType))
        {
            source = source.Where(t => t.MaterialType == materialType);
        }

        var total = await source.CountAsync(ct);
        var items = await source
            .OrderBy(t => t.ProductName)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<TileResponse>(
            items.Select(t => t.ToResponse()).ToList(), query.Page, query.PageSize, total));
    }

    [HttpGet("tiles/{id:guid}")]
    [HasPermission(Permissions.CatalogRead)]
    public async Task<ActionResult<TileResponse>> GetTile(Guid id, CancellationToken ct)
    {
        var tile = await db.Tiles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                   ?? throw new NotFoundException(nameof(Tile), id);
        return Ok(tile.ToResponse());
    }

    [HttpPost("tiles")]
    [HasPermission(Permissions.CatalogManage)]
    [ProducesResponseType(typeof(TileResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TileResponse>> CreateTile(SaveTileRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();
        await EnsureUniqueSkuAsync<Tile>(request.Sku, null, ct);

        var tile = Tile.Create(organizationId, request.Sku, request.ProductName,
            CustomersController.ParseEnum<TileMaterialType>(request.MaterialType, nameof(request.MaterialType)),
            request.LengthInches, request.WidthInches, request.TilesPerBox,
            request.CostPerSqFt, request.SellingPricePerSqFt);

        ApplyTileDetails(tile, request);

        db.Tiles.Add(tile);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Tile),
            tile.Id.ToString(), $"Tile {tile.Sku} added.", ct);

        return CreatedAtAction(nameof(GetTile), new { id = tile.Id }, tile.ToResponse());
    }

    [HttpPut("tiles/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<TileResponse>> UpdateTile(Guid id, SaveTileRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tile = await db.Tiles.FirstOrDefaultAsync(t => t.Id == id, ct)
                   ?? throw new NotFoundException(nameof(Tile), id);

        await EnsureUniqueSkuAsync<Tile>(request.Sku, id, ct);

        var oldPricing = new { tile.CostPerSqFt, tile.SellingPricePerSqFt };

        tile.SetSku(request.Sku);
        tile.SetProductName(request.ProductName);
        tile.SetMaterialType(
            CustomersController.ParseEnum<TileMaterialType>(request.MaterialType, nameof(request.MaterialType)));
        tile.UpdateDimensions(request.LengthInches, request.WidthInches, request.TilesPerBox);
        tile.UpdatePricing(request.CostPerSqFt, request.SellingPricePerSqFt);
        ApplyTileDetails(tile, request);

        await db.SaveChangesAsync(ct);

        // A price change is audited on its own, because it moves money on future estimates.
        if (oldPricing.CostPerSqFt != tile.CostPerSqFt ||
            oldPricing.SellingPricePerSqFt != tile.SellingPricePerSqFt)
        {
            await audit.RecordChangeAsync(tile.OrganizationId, AuditAction.PriceChange, nameof(Tile),
                tile.Id.ToString(), oldPricing,
                new { tile.CostPerSqFt, tile.SellingPricePerSqFt },
                $"Pricing changed for tile {tile.Sku}.", ct);
        }

        return Ok(tile.ToResponse());
    }

    [HttpDelete("tiles/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTile(Guid id, CancellationToken ct)
    {
        var tile = await db.Tiles.FirstOrDefaultAsync(t => t.Id == id, ct)
                   ?? throw new NotFoundException(nameof(Tile), id);

        // Soft delete only. Existing estimate lines keep their own price snapshot and are
        // unaffected, which is exactly why they hold one.
        db.Tiles.Remove(tile);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(tile.OrganizationId, AuditAction.Delete, nameof(Tile),
            id.ToString(), $"Tile {tile.Sku} removed.", ct);

        return NoContent();
    }

    // --- Materials ----------------------------------------------------------------------------

    [HttpGet("materials")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(PagedResult<MaterialResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MaterialResponse>>> ListMaterials(
        [FromQuery] CatalogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = db.Materials.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(m => m.Sku.Contains(term) || m.Name.Contains(term));
        }

        if (query.Active.HasValue)
        {
            source = source.Where(m => m.Active == query.Active.Value);
        }

        if (Enum.TryParse<MaterialCategory>(query.Category, true, out var category))
        {
            source = source.Where(m => m.Category == category);
        }

        var total = await source.CountAsync(ct);

        var rows = await source
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new { Material = m, SupplierName = m.Supplier!.Name })
            .ToListAsync(ct);

        return Ok(new PagedResult<MaterialResponse>(
            rows.Select(r => r.Material.ToResponse(r.SupplierName)).ToList(),
            query.Page, query.PageSize, total));
    }

    [HttpGet("materials/{id:guid}")]
    [HasPermission(Permissions.CatalogRead)]
    public async Task<ActionResult<MaterialResponse>> GetMaterial(Guid id, CancellationToken ct)
    {
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Material), id);
        return Ok(material.ToResponse());
    }

    [HttpPost("materials")]
    [HasPermission(Permissions.CatalogManage)]
    [ProducesResponseType(typeof(MaterialResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<MaterialResponse>> CreateMaterial(SaveMaterialRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();
        await EnsureUniqueSkuAsync<Material>(request.Sku, null, ct);
        await ValidateSupplierAsync(request.SupplierId, ct);

        var material = Material.Create(organizationId, request.Sku, request.Name,
            CustomersController.ParseEnum<MaterialCategory>(request.Category, nameof(request.Category)),
            request.Unit, request.Coverage, request.Cost, request.SellingPrice);

        material.Update(request.Sku, request.Name,
            CustomersController.ParseEnum<MaterialCategory>(request.Category, nameof(request.Category)),
            request.Unit, request.Coverage, request.Cost, request.SellingPrice,
            request.SupplierId, request.Description, request.Active);

        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Material),
            material.Id.ToString(), $"Material {material.Sku} added.", ct);

        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, material.ToResponse());
    }

    [HttpPut("materials/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<MaterialResponse>> UpdateMaterial(Guid id,
        SaveMaterialRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Material), id);

        await EnsureUniqueSkuAsync<Material>(request.Sku, id, ct);
        await ValidateSupplierAsync(request.SupplierId, ct);

        var oldPricing = new { material.Cost, material.SellingPrice, material.Coverage };

        material.Update(request.Sku, request.Name,
            CustomersController.ParseEnum<MaterialCategory>(request.Category, nameof(request.Category)),
            request.Unit, request.Coverage, request.Cost, request.SellingPrice,
            request.SupplierId, request.Description, request.Active);

        await db.SaveChangesAsync(ct);

        if (oldPricing.Cost != material.Cost || oldPricing.SellingPrice != material.SellingPrice ||
            oldPricing.Coverage != material.Coverage)
        {
            await audit.RecordChangeAsync(material.OrganizationId, AuditAction.PriceChange,
                nameof(Material), material.Id.ToString(), oldPricing,
                new { material.Cost, material.SellingPrice, material.Coverage },
                $"Pricing or coverage changed for material {material.Sku}.", ct);
        }

        return Ok(material.ToResponse());
    }

    [HttpDelete("materials/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMaterial(Guid id, CancellationToken ct)
    {
        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Material), id);

        if (await db.AssemblyItems.AnyAsync(i => i.MaterialId == id, ct))
        {
            throw new ConflictException(
                "This material is used by an assembly. Remove it from the assembly first, or deactivate it instead.");
        }

        db.Materials.Remove(material);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Patterns ------------------------------------------------------------------------------

    [HttpGet("patterns")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(IReadOnlyList<PatternResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PatternResponse>>> ListPatterns(CancellationToken ct)
    {
        var patterns = await db.Patterns.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);

        return Ok(patterns.Select(p => p.ToResponse()).ToList());
    }

    [HttpPost("patterns")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<PatternResponse>> CreatePattern(SavePatternRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var pattern = Pattern.Create(organizationId, request.Name, request.Description,
            request.DefaultWastePercentage, request.SortOrder);

        db.Patterns.Add(pattern);
        await db.SaveChangesAsync(ct);

        return Ok(pattern.ToResponse());
    }

    [HttpPut("patterns/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<PatternResponse>> UpdatePattern(Guid id,
        SavePatternRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pattern = await db.Patterns.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Pattern), id);

        var oldWaste = pattern.DefaultWastePercentage;
        pattern.Update(request.Name, request.Description, request.DefaultWastePercentage,
            request.Active, request.SortOrder);

        await db.SaveChangesAsync(ct);

        if (oldWaste != pattern.DefaultWastePercentage)
        {
            await audit.RecordChangeAsync(pattern.OrganizationId, AuditAction.Update, nameof(Pattern),
                pattern.Id.ToString(), new { DefaultWastePercentage = oldWaste },
                new { pattern.DefaultWastePercentage },
                $"Default waste for pattern '{pattern.Name}' changed. Existing estimates are unaffected.", ct);
        }

        return Ok(pattern.ToResponse());
    }

    [HttpDelete("patterns/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> DeletePattern(Guid id, CancellationToken ct)
    {
        var pattern = await db.Patterns.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Pattern), id);

        db.Patterns.Remove(pattern);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Waste rules ----------------------------------------------------------------------------

    [HttpGet("waste-rules")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(IReadOnlyList<WasteRuleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WasteRuleResponse>>> ListWasteRules(CancellationToken ct)
    {
        var rows = await db.WasteRules.AsNoTracking()
            .OrderByDescending(w => w.Priority).ThenBy(w => w.Name)
            .Select(w => new { Rule = w, PatternName = w.Pattern!.Name })
            .ToListAsync(ct);

        return Ok(rows.Select(r => r.Rule.ToResponse(r.PatternName)).ToList());
    }

    [HttpPost("waste-rules")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<WasteRuleResponse>> CreateWasteRule(SaveWasteRuleRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var rule = WasteRule.Create(organizationId, request.Name, request.WastePercentage,
            request.Priority, request.PatternId,
            ParseOptionalEnum<SurfaceType>(request.SurfaceType),
            ParseOptionalEnum<TileMaterialType>(request.TileMaterialType),
            ParseOptionalEnum<RoomType>(request.RoomType));

        db.WasteRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok(rule.ToResponse());
    }

    [HttpPut("waste-rules/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<WasteRuleResponse>> UpdateWasteRule(Guid id,
        SaveWasteRuleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await db.WasteRules.FirstOrDefaultAsync(w => w.Id == id, ct)
                   ?? throw new NotFoundException(nameof(WasteRule), id);

        rule.Update(request.Name, request.WastePercentage, request.Priority, request.PatternId,
            ParseOptionalEnum<SurfaceType>(request.SurfaceType),
            ParseOptionalEnum<TileMaterialType>(request.TileMaterialType),
            ParseOptionalEnum<RoomType>(request.RoomType),
            request.Active);

        await db.SaveChangesAsync(ct);
        return Ok(rule.ToResponse());
    }

    [HttpDelete("waste-rules/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> DeleteWasteRule(Guid id, CancellationToken ct)
    {
        var rule = await db.WasteRules.FirstOrDefaultAsync(w => w.Id == id, ct)
                   ?? throw new NotFoundException(nameof(WasteRule), id);

        db.WasteRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Labor rates -----------------------------------------------------------------------------

    [HttpGet("labor-rates")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(IReadOnlyList<LaborRateResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LaborRateResponse>>> ListLaborRates(CancellationToken ct)
    {
        var rates = await db.LaborRates.AsNoTracking()
            .OrderBy(l => l.Trade).ThenBy(l => l.Name)
            .ToListAsync(ct);

        return Ok(rates.Select(l => l.ToResponse()).ToList());
    }

    [HttpPost("labor-rates")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<LaborRateResponse>> CreateLaborRate(SaveLaborRateRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var rate = LaborRate.Create(organizationId, request.Name, request.Trade, request.Unit,
            CustomersController.ParseEnum<LaborCalculationMethod>(
                request.CalculationMethod, nameof(request.CalculationMethod)),
            request.Rate, request.Productivity);

        db.LaborRates.Add(rate);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(LaborRate),
            rate.Id.ToString(), $"Labor rate '{rate.Name}' added.", ct);

        return Ok(rate.ToResponse());
    }

    [HttpPut("labor-rates/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<LaborRateResponse>> UpdateLaborRate(Guid id,
        SaveLaborRateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rate = await db.LaborRates.FirstOrDefaultAsync(l => l.Id == id, ct)
                   ?? throw new NotFoundException(nameof(LaborRate), id);

        var oldRate = new { rate.Rate, rate.Productivity, rate.CalculationMethod };

        rate.Update(request.Name, request.Trade, request.Unit,
            CustomersController.ParseEnum<LaborCalculationMethod>(
                request.CalculationMethod, nameof(request.CalculationMethod)),
            request.Rate, request.Productivity, request.Description, request.Active);

        await db.SaveChangesAsync(ct);

        if (oldRate.Rate != rate.Rate || oldRate.Productivity != rate.Productivity)
        {
            await audit.RecordChangeAsync(rate.OrganizationId, AuditAction.PriceChange,
                nameof(LaborRate), rate.Id.ToString(), oldRate,
                new { rate.Rate, rate.Productivity, rate.CalculationMethod },
                $"Labor rate '{rate.Name}' changed. Existing estimates keep their snapshot.", ct);
        }

        return Ok(rate.ToResponse());
    }

    [HttpDelete("labor-rates/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> DeleteLaborRate(Guid id, CancellationToken ct)
    {
        var rate = await db.LaborRates.FirstOrDefaultAsync(l => l.Id == id, ct)
                   ?? throw new NotFoundException(nameof(LaborRate), id);

        if (await db.AssemblyItems.AnyAsync(i => i.LaborRateId == id, ct))
        {
            throw new ConflictException(
                "This labor rate is used by an assembly. Remove it from the assembly first, or deactivate it instead.");
        }

        db.LaborRates.Remove(rate);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Assemblies -------------------------------------------------------------------------------

    [HttpGet("assemblies")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AssemblyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssemblyResponse>>> ListAssemblies(CancellationToken ct)
    {
        var assemblies = await db.Assemblies.AsNoTracking()
            .Include(a => a.Items)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var materialNames = await db.Materials.AsNoTracking()
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var laborNames = await db.LaborRates.AsNoTracking()
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return Ok(assemblies.Select(a => a.ToResponse(materialNames, laborNames)).ToList());
    }

    [HttpGet("assemblies/{id:guid}")]
    [HasPermission(Permissions.CatalogRead)]
    public async Task<ActionResult<AssemblyResponse>> GetAssembly(Guid id, CancellationToken ct)
    {
        var assembly = await db.Assemblies.AsNoTracking()
                           .Include(a => a.Items)
                           .FirstOrDefaultAsync(a => a.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Assembly), id);

        var materialNames = await db.Materials.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var laborNames = await db.LaborRates.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return Ok(assembly.ToResponse(materialNames, laborNames));
    }

    [HttpPost("assemblies")]
    [HasPermission(Permissions.CatalogManage)]
    [ProducesResponseType(typeof(AssemblyResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AssemblyResponse>> CreateAssembly(SaveAssemblyRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();

        var assembly = Assembly.Create(organizationId, request.Name, request.Description,
            ParseOptionalEnum<SurfaceType>(request.AppliesToSurfaceType));

        db.Assemblies.Add(assembly);
        await ReplaceAssemblyItemsAsync(assembly, request.Items, ct);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAssembly), new { id = assembly.Id }, assembly.ToResponse());
    }

    [HttpPut("assemblies/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<AssemblyResponse>> UpdateAssembly(Guid id,
        SaveAssemblyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assembly = await db.Assemblies.Include(a => a.Items)
                           .FirstOrDefaultAsync(a => a.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Assembly), id);

        assembly.Update(request.Name, request.Description,
            ParseOptionalEnum<SurfaceType>(request.AppliesToSurfaceType), request.Active);

        db.AssemblyItems.RemoveRange(assembly.Items.ToList());
        assembly.Items.Clear();

        await ReplaceAssemblyItemsAsync(assembly, request.Items, ct);
        await db.SaveChangesAsync(ct);

        return Ok(assembly.ToResponse());
    }

    [HttpDelete("assemblies/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> DeleteAssembly(Guid id, CancellationToken ct)
    {
        var assembly = await db.Assemblies.FirstOrDefaultAsync(a => a.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Assembly), id);

        db.Assemblies.Remove(assembly);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Suppliers ----------------------------------------------------------------------------------

    [HttpGet("suppliers")]
    [HasPermission(Permissions.CatalogRead)]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> ListSuppliers(CancellationToken ct)
    {
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(suppliers.Select(s => s.ToResponse()).ToList());
    }

    [HttpPost("suppliers")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<SupplierResponse>> CreateSupplier(SaveSupplierRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();
        var supplier = Supplier.Create(organizationId, request.Name);

        supplier.Update(request.Name, request.ContactName, request.Email, request.Phone,
            request.AccountNumber, request.Address.ToDomain(), request.Active);

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        return Ok(supplier.ToResponse());
    }

    [HttpPut("suppliers/{id:guid}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<ActionResult<SupplierResponse>> UpdateSupplier(Guid id,
        SaveSupplierRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Supplier), id);

        supplier.Update(request.Name, request.ContactName, request.Email, request.Phone,
            request.AccountNumber, request.Address.ToDomain(), request.Active);

        await db.SaveChangesAsync(ct);
        return Ok(supplier.ToResponse());
    }

    // --- Helpers -------------------------------------------------------------------------------------

    private static void ApplyTileDetails(Tile tile, SaveTileRequest request)
    {
        tile.UpdateDetails(request.Brand, request.Collection, request.Finish, request.Color,
            request.Description, request.ThicknessInches, request.Active);

        if (request.SqFtPerBoxOverride.HasValue)
        {
            tile.OverrideSqFtPerBox(request.SqFtPerBoxOverride.Value);
        }
    }

    private async Task ReplaceAssemblyItemsAsync(Assembly assembly,
        IReadOnlyList<SaveAssemblyItemRequest> items, CancellationToken ct)
    {
        var created = new List<AssemblyItem>();
        var sortOrder = 0;

        foreach (var item in items)
        {
            // Resolving through the tenant-filtered sets means an id from another organization
            // simply is not found here.
            if (item.MaterialId.HasValue &&
                !await db.Materials.AnyAsync(m => m.Id == item.MaterialId.Value, ct))
            {
                throw new ValidationFailedException(nameof(item.MaterialId),
                    "That material was not found in your catalog.");
            }

            if (item.LaborRateId.HasValue &&
                !await db.LaborRates.AnyAsync(l => l.Id == item.LaborRateId.Value, ct))
            {
                throw new ValidationFailedException(nameof(item.LaborRateId),
                    "That labor rate was not found.");
            }

            var entity = AssemblyItem.Create(assembly.OrganizationId, assembly.Id,
                item.MaterialId, item.LaborRateId, item.UsesSurfaceTile,
                CustomersController.ParseEnum<QuantityMethod>(item.QuantityMethod, nameof(item.QuantityMethod)),
                item.Factor, item.FixedQuantity, item.CoverageOverride, item.Unit, sortOrder++);

            entity.Update(item.MaterialId, item.LaborRateId, item.UsesSurfaceTile,
                CustomersController.ParseEnum<QuantityMethod>(item.QuantityMethod, nameof(item.QuantityMethod)),
                item.Factor, item.FixedQuantity, item.CoverageOverride,
                item.WasteOverridePercentage, item.Unit, item.Description, entity.SortOrder);

            db.AssemblyItems.Add(entity);
            created.Add(entity);
        }

        // The navigation is filled from the same instances after they are tracked, so mapping a
        // response does not need another query.
        foreach (var item in created)
        {
            assembly.Items.Add(item);
        }
    }

    private async Task ValidateSupplierAsync(Guid? supplierId, CancellationToken ct)
    {
        if (supplierId.HasValue && !await db.Suppliers.AnyAsync(s => s.Id == supplierId.Value, ct))
        {
            throw new ValidationFailedException(nameof(supplierId), "That supplier was not found.");
        }
    }

    /// <summary>
    /// SKUs are unique per organization. The database enforces it too, but checking here turns a
    /// constraint violation into a clear field-level message.
    /// </summary>
    private async Task EnsureUniqueSkuAsync<TEntity>(string sku, Guid? excludeId, CancellationToken ct)
        where TEntity : class
    {
        var normalized = sku.Trim();

        var exists = typeof(TEntity) == typeof(Tile)
            ? await db.Tiles.AnyAsync(t => t.Sku == normalized && (excludeId == null || t.Id != excludeId), ct)
            : await db.Materials.AnyAsync(m => m.Sku == normalized && (excludeId == null || m.Id != excludeId), ct);

        if (exists)
        {
            throw new ValidationFailedException("sku", $"SKU '{normalized}' is already in use.");
        }
    }

    private static TEnum? ParseOptionalEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
}
