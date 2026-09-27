using BindingFlags = System.Reflection.BindingFlags;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Application.Abstractions;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Common;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.Quotes;
using TileEstimator.Domain.SystemModule;

namespace TileEstimator.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. Tenant isolation is structural here, not something each query
/// has to remember: every <see cref="ITenantOwned"/> entity gets a global query filter, and
/// <see cref="TenantSaveChangesInterceptor"/> stamps and guards writes.
/// </summary>
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentOrganizationService currentOrganization)
    : DbContext(options), IApplicationDbContext
{
    private readonly ICurrentOrganizationService _currentOrganization = currentOrganization;

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    // Customers
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Surface> Surfaces => Set<Surface>();
    public DbSet<Opening> Openings => Set<Opening>();
    public DbSet<ProjectNote> ProjectNotes => Set<ProjectNote>();
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();

    // Catalog
    public DbSet<Tile> Tiles => Set<Tile>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();

    // Estimation
    public DbSet<Pattern> Patterns => Set<Pattern>();
    public DbSet<WasteRule> WasteRules => Set<WasteRule>();
    public DbSet<LaborRate> LaborRates => Set<LaborRate>();
    public DbSet<Domain.Estimation.Assembly> Assemblies => Set<Domain.Estimation.Assembly>();
    public DbSet<AssemblyItem> AssemblyItems => Set<AssemblyItem>();
    public DbSet<Takeoff> Takeoffs => Set<Takeoff>();
    public DbSet<TakeoffItem> TakeoffItems => Set<TakeoffItem>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateLine> EstimateLines => Set<EstimateLine>();

    // Quotes
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<QuoteRecipient> QuoteRecipients => Set<QuoteRecipient>();
    public DbSet<QuoteApproval> QuoteApprovals => Set<QuoteApproval>();
    public DbSet<ChangeOrder> ChangeOrders => Set<ChangeOrder>();

    // System
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplyGlobalFilters(modelBuilder);
    }

    /// <summary>
    /// Adds, to every entity type, the filters that make isolation automatic:
    /// tenant scoping for <see cref="ITenantOwned"/> and hiding of soft-deleted rows.
    /// Conceptually every query becomes <c>WHERE OrganizationId = @current AND IsDeleted = 0</c>.
    /// The filters are applied through the generic helpers below so the lambdas reference this
    /// context instance the way EF Core expects, and each request sees its own organization.
    /// </summary>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Owned types are filtered through their owner.
            if (entityType.IsOwned())
            {
                continue;
            }

            var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            var methodName = (isTenantOwned, isSoftDeletable) switch
            {
                (true, true) => nameof(ApplyTenantAndSoftDeleteFilter),
                (true, false) => nameof(ApplyTenantFilter),
                (false, true) => nameof(ApplySoftDeleteFilter),
                _ => null
            };

            if (methodName is null)
            {
                continue;
            }

            typeof(ApplicationDbContext)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(clrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            TenantFilterBypassed || e.OrganizationId == CurrentOrganizationId);

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            (TenantFilterBypassed || e.OrganizationId == CurrentOrganizationId) && !e.IsDeleted);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);

    /// <summary>Read by the global query filters. Not part of the public API.</summary>
    public Guid? CurrentOrganizationId => _currentOrganization.OrganizationId;

    /// <summary>Read by the global query filters. Not part of the public API.</summary>
    public bool TenantFilterBypassed => _currentOrganization.IsTenantFilterBypassed;

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        // The retrying execution strategy refuses a user-initiated transaction unless the whole
        // unit is handed to it, so that the transaction is replayed as a whole on a retry.
        var strategy = Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            await work(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }
}
