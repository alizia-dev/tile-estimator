using Microsoft.EntityFrameworkCore;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Infrastructure.Persistence.Seeding;

/// <summary>
/// Builds the default estimation configuration for a brand-new organization (SPEC 5): patterns,
/// waste rules, materials, labor rates and assemblies. Everything it creates is an ordinary
/// tenant-owned row the contractor can then edit or delete.
/// </summary>
public sealed class OrganizationProvisioner(ApplicationDbContext db)
{
    public async Task ProvisionDefaultsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var patterns = OrganizationDefaults.CreatePatterns(organizationId);
        db.Patterns.AddRange(patterns);

        var materials = OrganizationDefaults.CreateMaterials(organizationId);
        db.Materials.AddRange(materials);

        var laborRates = OrganizationDefaults.CreateLaborRates(organizationId);
        db.LaborRates.AddRange(laborRates);

        // Waste rules may point at a pattern, so the patterns must exist first.
        await db.SaveChangesAsync(cancellationToken);

        var straightLay = patterns.First(p => p.Name == "Straight Lay");
        var wasteRules = OrganizationDefaults.CreateWasteRules(organizationId).ToList();
        wasteRules.Add(WasteRule.Create(organizationId, "Straight lay baseline", 10m, 5,
            straightLay.Id, null, null, null));
        db.WasteRules.AddRange(wasteRules);

        BuildAssemblies(organizationId, materials, laborRates);

        await db.SaveChangesAsync(cancellationToken);
    }

    private void BuildAssemblies(
        Guid organizationId,
        IReadOnlyList<Domain.Catalog.Material> materials,
        IReadOnlyList<LaborRate> laborRates)
    {
        var materialsBySku = materials.ToDictionary(m => m.Sku, StringComparer.Ordinal);
        var laborByName = laborRates.ToDictionary(l => l.Name, StringComparer.Ordinal);

        foreach (var definition in OrganizationDefaults.AssemblyDefinitions)
        {
            var assembly = Assembly.Create(organizationId, definition.Name, definition.Description,
                definition.AppliesTo);
            db.Assemblies.Add(assembly);

            var sortOrder = 0;
            foreach (var item in definition.Items)
            {
                Guid? materialId = null;
                Guid? laborRateId = null;
                var unit = item.Unit;

                if (item.MaterialSku is not null)
                {
                    // A seed assembly that names a SKU we did not seed is a bug in the seed data,
                    // not something to paper over with a null material.
                    if (!materialsBySku.TryGetValue(item.MaterialSku, out var material))
                    {
                        throw new InvalidOperationException(
                            $"Seed assembly '{definition.Name}' references unknown material SKU '{item.MaterialSku}'.");
                    }
                    materialId = material.Id;
                    unit ??= material.Unit;
                }
                else if (item.LaborRateName is not null)
                {
                    if (!laborByName.TryGetValue(item.LaborRateName, out var labor))
                    {
                        throw new InvalidOperationException(
                            $"Seed assembly '{definition.Name}' references unknown labor rate '{item.LaborRateName}'.");
                    }
                    laborRateId = labor.Id;
                    unit ??= labor.Unit;
                }
                else
                {
                    unit ??= UnitOfMeasure.SquareFeet;
                }

                db.AssemblyItems.Add(AssemblyItem.Create(
                    organizationId,
                    assembly.Id,
                    materialId,
                    laborRateId,
                    item.UsesSurfaceTile,
                    item.QuantityMethod,
                    item.Factor,
                    item.FixedQuantity,
                    coverageOverride: null,
                    unit,
                    sortOrder++));
            }
        }
    }

    /// <summary>True when this organization already has its defaults, so provisioning is skipped.</summary>
    public Task<bool> HasDefaultsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Patterns.IgnoreQueryFilters().AnyAsync(p => p.OrganizationId == organizationId, cancellationToken);
}
