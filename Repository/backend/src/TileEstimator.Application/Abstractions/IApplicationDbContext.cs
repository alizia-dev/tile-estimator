using Microsoft.EntityFrameworkCore;
using TileEstimator.Domain.Catalog;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Estimation;
using TileEstimator.Domain.Identity;
using TileEstimator.Domain.Organizations;
using TileEstimator.Domain.Projects;
using TileEstimator.Domain.Quotes;
using TileEstimator.Domain.SystemModule;

namespace TileEstimator.Application.Abstractions;

/// <summary>
/// The persistence surface the Application layer works against. Application depends on this
/// interface, not on the concrete context, which keeps the dependency direction pointing inward.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserToken> UserTokens { get; }

    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<OrganizationSettings> OrganizationSettings { get; }
    DbSet<UserInvitation> UserInvitations { get; }

    DbSet<Customer> Customers { get; }
    DbSet<CustomerContact> CustomerContacts { get; }

    DbSet<Project> Projects { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Surface> Surfaces { get; }
    DbSet<Opening> Openings { get; }
    DbSet<ProjectNote> ProjectNotes { get; }
    DbSet<ProjectDocument> ProjectDocuments { get; }

    DbSet<Tile> Tiles { get; }
    DbSet<Material> Materials { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProduct> SupplierProducts { get; }
    DbSet<PriceList> PriceLists { get; }
    DbSet<PriceListItem> PriceListItems { get; }

    DbSet<Pattern> Patterns { get; }
    DbSet<WasteRule> WasteRules { get; }
    DbSet<LaborRate> LaborRates { get; }
    DbSet<Domain.Estimation.Assembly> Assemblies { get; }
    DbSet<AssemblyItem> AssemblyItems { get; }
    DbSet<Takeoff> Takeoffs { get; }
    DbSet<TakeoffItem> TakeoffItems { get; }
    DbSet<Estimate> Estimates { get; }
    DbSet<EstimateLine> EstimateLines { get; }

    DbSet<Quote> Quotes { get; }
    DbSet<QuoteLine> QuoteLines { get; }
    DbSet<QuoteRecipient> QuoteRecipients { get; }
    DbSet<QuoteApproval> QuoteApprovals { get; }
    DbSet<ChangeOrder> ChangeOrders { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a single transaction, committing when it returns and
    /// rolling back if it throws. Used for multi-step writes such as the SPEC 5 registration flow.
    /// <para>
    /// The implementation runs the whole block through the provider's execution strategy, so a
    /// transient connection failure retries the entire unit rather than leaving it half applied.
    /// Because a retry replays the block, the work must be safe to run more than once.
    /// </para>
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
