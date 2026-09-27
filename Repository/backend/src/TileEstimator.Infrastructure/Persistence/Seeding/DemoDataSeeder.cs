using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.Projects;

namespace TileEstimator.Infrastructure.Persistence.Seeding;

/// <summary>
/// SPEC 22 demo data: the "Demo Tile Company" organization with a sample catalog, customer,
/// project and rooms, so a fresh checkout has something to look at.
/// <para>DEVELOPMENT ONLY. Guarded by Database:SeedDemoData and never enabled in production.</para>
/// </summary>
public sealed class DemoDataSeeder(
    ApplicationDbContext db,
    OrganizationProvisioner provisioner,
    ICurrentOrganizationService currentOrganization,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    public const string DemoOrganizationName = "Demo Tile Company";
    public const string DemoUserEmail = "demo@tileestimator.local";
    public const string DemoPassword = "Demo123!Pass";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // The demo organization does not exist yet, so there is nothing to scope queries to.
        using var _ = currentOrganization.BypassTenantFilter();

        var existing = await db.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Name == DemoOrganizationName, cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation("Demo data already present; skipping.");
            return;
        }

        var now = clock.UtcNow;

        var organization = Organization.Create(DemoOrganizationName);
        organization.UpdateProfile(
            DemoOrganizationName, "Demo Tile Company LLC", "office@demotile.local", "(555) 010-2030",
            "https://demotile.local", "CSLB-1029384",
            Address.Create("1420 Industrial Way", "Suite 200", "Phoenix", "AZ", "85004"));
        db.Organizations.Add(organization);

        var settings = OrganizationSettings.CreateDefault(organization.Id);
        settings.UpdatePricingDefaults(8.6m, TaxBasis.MaterialsOnly, 10m, PricingStrategy.Markup, 22m, 25m, true);
        settings.UpdateDocumentDefaults(30, null,
            "Payment terms: 50% deposit on acceptance, balance on completion. "
            + "This quote is valid for 30 days. Prices assume the substrate is sound and level; "
            + "additional preparation will be quoted separately as a change order.",
            "Thank you for the opportunity to quote your project.");
        db.OrganizationSettings.Add(settings);

        var ownerRoleId = await db.Roles
            .Where(r => r.Name == Roles.Owner)
            .Select(r => r.Id)
            .FirstAsync(cancellationToken);

        var user = User.Create(DemoUserEmail, passwordHasher.Hash(DemoPassword), "Dana", "Rivera");
        user.ConfirmEmail();
        db.Users.Add(user);

        db.OrganizationMembers.Add(
            OrganizationMember.Create(organization.Id, user.Id, ownerRoleId, now));

        await db.SaveChangesAsync(cancellationToken);

        await provisioner.ProvisionDefaultsAsync(organization.Id, cancellationToken);

        var tiles = CreateDemoTiles(organization.Id);
        db.Tiles.AddRange(tiles);

        var customer = Customer.Create(organization.Id, "CUST-1001", CustomerType.Residential,
            "Morgan", "Ellis", null, "morgan.ellis@example.com", "(555) 867-5309");
        customer.Update(CustomerType.Residential, "Morgan", "Ellis", null,
            "morgan.ellis@example.com", "(555) 867-5309",
            "Referred by a previous client. Prefers porcelain.", CustomerStatus.Active,
            Address.Create("88 Juniper Lane", null, "Scottsdale", "AZ", "85251"),
            Address.Create("88 Juniper Lane", null, "Scottsdale", "AZ", "85251"));
        db.Customers.Add(customer);

        await db.SaveChangesAsync(cancellationToken);

        await CreateDemoProjectAsync(organization.Id, customer.Id, tiles, cancellationToken);

        logger.LogInformation(
            "Demo data seeded. Sign in with {Email} / {Password}.", DemoUserEmail, DemoPassword);
    }

    private static List<Tile> CreateDemoTiles(Guid organizationId)
    {
        var porcelain = Tile.Create(organizationId, "PORC-1224-GRY", "Urban Grey 12x24 Porcelain",
            TileMaterialType.Porcelain, 12m, 24m, 8, 2.85m, 5.50m);
        porcelain.UpdateDetails("Terrafina", "Urban", "Matte", "Grey",
            "Rectified large-format porcelain suitable for floors and walls.", 0.375m, true);

        var subway = Tile.Create(organizationId, "CER-0306-WHT", "Classic White 3x6 Subway",
            TileMaterialType.Ceramic, 3m, 6m, 80, 0.95m, 2.40m);
        subway.UpdateDetails("Terrafina", "Classic", "Gloss", "White",
            "Traditional subway tile for backsplashes and shower walls.", 0.25m, true);

        var mosaic = Tile.Create(organizationId, "MOS-1212-HEX", "Hex Mosaic 12x12 Sheet",
            TileMaterialType.Mosaic, 12m, 12m, 10, 6.20m, 12.00m);
        mosaic.UpdateDetails("Terrafina", "Hex", "Matte", "White",
            "Mesh-backed hex mosaic, typical for shower floors.", 0.25m, true);

        var marble = Tile.Create(organizationId, "MAR-1212-CAR", "Carrara 12x12 Marble",
            TileMaterialType.Marble, 12m, 12m, 10, 9.40m, 17.50m);
        marble.UpdateDetails("StoneWorks", "Carrara", "Polished", "White/Grey",
            "Natural marble. Expect higher breakage and more waste.", 0.4m, true);

        return [porcelain, subway, mosaic, marble];
    }

    private async Task CreateDemoProjectAsync(Guid organizationId, Guid customerId,
        IReadOnlyList<Tile> tiles, CancellationToken cancellationToken)
    {
        var project = Project.Create(organizationId, customerId, "PRJ-1001",
            "Ellis Master Bathroom Remodel", ProjectType.Remodel,
            "Full gut and retile of the master bathroom: floor, shower and vanity backsplash.",
            Address.Create("88 Juniper Lane", null, "Scottsdale", "AZ", "85251"));
        project.ChangeStatus(ProjectStatus.Estimating);
        db.Projects.Add(project);

        var porcelain = tiles.First(t => t.Sku == "PORC-1224-GRY");
        var subway = tiles.First(t => t.Sku == "CER-0306-WHT");
        var mosaic = tiles.First(t => t.Sku == "MOS-1212-HEX");

        var patterns = await db.Patterns.IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var assemblies = await db.Assemblies.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var straightLay = patterns.First(p => p.Name == "Straight Lay").Id;
        var runningBond = patterns.First(p => p.Name == "Running Bond").Id;

        Guid AssemblyId(string name) => assemblies.First(a => a.Name == name).Id;

        var bathroom = Room.Create(organizationId, project.Id, "Master Bathroom",
            RoomType.MasterBathroom, null, 1);
        db.Rooms.Add(bathroom);

        // Floor: 12 x 15, which is the SPEC 14 reference case.
        var floor = Surface.Create(organizationId, bathroom.Id, "Bathroom Floor",
            SurfaceType.Floor, 12m, 15m, null, 1);
        floor.UpdateDimensions(12m, 15m, null, null, 34m);
        floor.UpdateSelections(porcelain.Id, straightLay, AssemblyId("Bathroom Floor"), 12m);
        db.Surfaces.Add(floor);
        db.Openings.Add(Opening.Create(organizationId, floor.Id, "Vanity footprint",
            OpeningType.Cabinet, 5m, 2m, 1));

        var showerWalls = Surface.Create(organizationId, bathroom.Id, "Shower Walls",
            SurfaceType.ShowerWall, 14m, null, 8m, 2);
        showerWalls.UpdateDimensions(14m, null, 8m, null, 28m);
        showerWalls.UpdateSelections(subway.Id, runningBond, AssemblyId("Shower Wall"), null);
        db.Surfaces.Add(showerWalls);
        db.Openings.Add(Opening.Create(organizationId, showerWalls.Id, "Shower niche",
            OpeningType.Niche, 2m, 1.25m, 1));
        db.Openings.Add(Opening.Create(organizationId, showerWalls.Id, "Shower door",
            OpeningType.Door, 2.5m, 7m, 1));

        var showerFloor = Surface.Create(organizationId, bathroom.Id, "Shower Floor",
            SurfaceType.ShowerFloor, 4m, 4m, null, 3);
        showerFloor.UpdateDimensions(4m, 4m, null, null, 0m);
        showerFloor.UpdateSelections(mosaic.Id, straightLay, AssemblyId("Shower Floor"), null);
        db.Surfaces.Add(showerFloor);

        var backsplash = Surface.Create(organizationId, bathroom.Id, "Vanity Backsplash",
            SurfaceType.Backsplash, 6m, null, 1.5m, 4);
        backsplash.UpdateDimensions(6m, null, 1.5m, null, 6m);
        backsplash.UpdateSelections(subway.Id, runningBond, AssemblyId("Backsplash"), null);
        db.Surfaces.Add(backsplash);

        db.ProjectNotes.Add(ProjectNote.Create(organizationId, project.Id,
            "Customer selected the 12x24 porcelain for the floor and classic subway for the shower walls."));

        await db.SaveChangesAsync(cancellationToken);
    }
}
