using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Organizations;
using TileEstimator.Infrastructure.Persistence;
using TileEstimator.Infrastructure.Services;
using Xunit;

namespace TileEstimator.IntegrationTests;

/// <summary>
/// SPEC 4 and SPEC 22. These are the tests that prove tenant isolation is structural: they talk
/// to a real <see cref="ApplicationDbContext"/> with its query filters and save interceptor in
/// place, and assert that a caller in one organization simply cannot see or touch another's rows.
/// </summary>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly string _databaseName = "tenancy-" + Guid.NewGuid();

    private CurrentOrganizationService _tenant = null!;

    public async Task InitializeAsync()
    {
        _tenant = new CurrentOrganizationService();

        // Seed both tenants with the filter suspended, the way registration and seeding do.
        await using var db = CreateContext(_tenant);
        using (_tenant.BypassTenantFilter())
        {
            db.Customers.Add(NewCustomer(_orgA, "CUST-A1", "Alice"));
            db.Customers.Add(NewCustomer(_orgA, "CUST-A2", "Aaron"));
            db.Customers.Add(NewCustomer(_orgB, "CUST-B1", "Bob"));

            db.Tiles.Add(NewTile(_orgA, "TILE-A"));
            db.Tiles.Add(NewTile(_orgB, "TILE-B"));

            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_query_returns_only_the_current_organizations_rows()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        var customers = await db.Customers.ToListAsync();

        customers.Should().HaveCount(2);
        customers.Should().OnlyContain(c => c.OrganizationId == _orgA);
        customers.Should().NotContain(c => c.CustomerNumber == "CUST-B1");
    }

    [Fact]
    public async Task Another_organizations_row_cannot_be_fetched_even_by_its_exact_id()
    {
        Guid otherId;
        _tenant.Set(_orgB, []);

        await using (var db = CreateContext(_tenant))
        {
            otherId = (await db.Customers.FirstAsync()).Id;
        }

        // Now ask for that exact id as organization A. Knowing the id must not be enough.
        _tenant.Set(_orgA, []);
        await using var asA = CreateContext(_tenant);

        var found = await asA.Customers.FirstOrDefaultAsync(c => c.Id == otherId);

        found.Should().BeNull("an id from another tenant must behave exactly like a row that does not exist");
    }

    [Fact]
    public async Task A_write_stamped_with_another_organizations_id_is_rejected()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        // A client cannot set OrganizationId through the API, but if a handler ever did, the
        // interceptor is the backstop.
        var smuggled = NewCustomer(_orgB, "CUST-SMUGGLE", "Mallory");
        db.Customers.Add(smuggled);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantViolationException>();
    }

    [Fact]
    public async Task A_new_row_is_stamped_with_the_current_organization_automatically()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        // OrganizationId left unset on purpose: the interceptor fills it in.
        var customer = Customer.Create(Guid.Empty, "CUST-A3", CustomerType.Residential,
            "Avery", "Nguyen", null, null, null);
        db.Customers.Add(customer);

        await db.SaveChangesAsync();

        customer.OrganizationId.Should().Be(_orgA);
    }

    [Fact]
    public async Task A_write_with_no_active_organization_is_refused_rather_than_written_loose()
    {
        await using var db = CreateContext(_tenant); // nothing set

        db.Customers.Add(Customer.Create(Guid.Empty, "CUST-ORPHAN", CustomerType.Residential,
            "No", "Tenant", null, null, null));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantViolationException>();
    }

    [Fact]
    public async Task Moving_an_existing_row_into_another_tenant_is_rejected()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        var customer = await db.Customers.FirstAsync();
        customer.OrganizationId = _orgB;

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantViolationException>();
    }

    [Fact]
    public async Task Deleting_a_business_record_hides_it_instead_of_removing_it()
    {
        _tenant.Set(_orgA, []);

        Guid deletedId;
        await using (var db = CreateContext(_tenant))
        {
            var customer = await db.Customers.FirstAsync(c => c.CustomerNumber == "CUST-A1");
            deletedId = customer.Id;
            db.Customers.Remove(customer);
            await db.SaveChangesAsync();
        }

        await using var verify = CreateContext(_tenant);

        (await verify.Customers.AnyAsync(c => c.Id == deletedId))
            .Should().BeFalse("a soft-deleted row is hidden from normal queries");

        var raw = await verify.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == deletedId);
        raw.IsDeleted.Should().BeTrue("the row is still there, flagged as deleted");
        raw.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task The_catalog_is_isolated_per_organization()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        var tiles = await db.Tiles.ToListAsync();

        tiles.Should().ContainSingle();
        tiles[0].Sku.Should().Be("TILE-A");
    }

    [Fact]
    public async Task Switching_the_active_organization_switches_the_visible_rows()
    {
        _tenant.Set(_orgA, []);
        await using (var asA = CreateContext(_tenant))
        {
            (await asA.Customers.CountAsync()).Should().Be(2);
        }

        _tenant.Set(_orgB, []);
        await using var asB = CreateContext(_tenant);

        (await asB.Customers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task An_explicit_bypass_sees_every_tenant_and_ends_with_the_scope()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        using (_tenant.BypassTenantFilter())
        {
            (await db.Customers.IgnoreQueryFilters().CountAsync()).Should().Be(3);
        }

        // Outside the scope the filter is back on.
        _tenant.IsTenantFilterBypassed.Should().BeFalse();
        (await db.Customers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Created_and_updated_stamps_are_written_in_UTC()
    {
        _tenant.Set(_orgA, []);
        await using var db = CreateContext(_tenant);

        var customer = Customer.Create(Guid.Empty, "CUST-STAMP", CustomerType.Residential,
            "Sam", "Stamp", null, null, null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        customer.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        customer.CreatedAt.Kind.Should().NotBe(DateTimeKind.Local);
    }

    // --- Helpers -----------------------------------------------------------------------------

    private ApplicationDbContext CreateContext(CurrentOrganizationService tenant)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(new TenantSaveChangesInterceptor(
                new StubCurrentUser(), tenant, new SystemClock(),
                NullLogger<TenantSaveChangesInterceptor>.Instance))
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options, tenant);
    }

    private static Customer NewCustomer(Guid organizationId, string number, string firstName) =>
        Customer.Create(organizationId, number, CustomerType.Residential, firstName, "Tester",
            null, $"{firstName.ToLowerInvariant()}@example.com", null);

    private static Tile NewTile(Guid organizationId, string sku) =>
        Tile.Create(organizationId, sku, "Test Tile", TileMaterialType.Porcelain, 12m, 12m, 10, 2m, 4m);

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => "tester@example.com";
        public bool IsAuthenticated => true;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "integration-tests";
        public string? CorrelationId => "test-correlation";
    }
}
