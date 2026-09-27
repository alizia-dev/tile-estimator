using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Common;
using TileEstimator.Domain.Common;

namespace TileEstimator.Infrastructure.Persistence;

/// <summary>
/// Makes tenancy and auditing structural rather than something every handler has to remember.
/// On each save it:
/// <list type="number">
///   <item>stamps CreatedAt/CreatedBy and UpdatedAt/UpdatedBy in UTC,</item>
///   <item>stamps OrganizationId on new tenant-owned rows,</item>
///   <item>rejects any insert or update that would write into another tenant,</item>
///   <item>turns deletes of soft-deletable rows into soft deletes.</item>
/// </list>
/// </summary>
public sealed class TenantSaveChangesInterceptor(
    ICurrentUserService currentUser,
    ICurrentOrganizationService currentOrganization,
    IDateTimeProvider clock,
    ILogger<TenantSaveChangesInterceptor> logger)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var organizationId = currentOrganization.OrganizationId;
        var bypassed = currentOrganization.IsTenantFilterBypassed;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            StampAudit(entry, now, userId);
            StampTenant(entry, organizationId, bypassed);
            ConvertHardDeleteToSoftDelete(entry, now, userId);
        }
    }

    private static void StampAudit(EntityEntry entry, DateTime now, Guid? userId)
    {
        if (entry.Entity is not IAuditable auditable)
        {
            return;
        }

        switch (entry.State)
        {
            case EntityState.Added:
                auditable.CreatedAt = now;
                auditable.CreatedBy ??= userId;
                break;

            case EntityState.Modified:
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = userId;
                // CreatedAt/CreatedBy are set once and never rewritten.
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                break;
        }
    }

    private void StampTenant(EntityEntry entry, Guid? organizationId, bool bypassed)
    {
        if (entry.Entity is not ITenantOwned tenantOwned)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            if (tenantOwned.OrganizationId == Guid.Empty)
            {
                // The client never supplies this. If there is no active organization and we are
                // not in an explicit bypass, the write is a bug and must not reach the database.
                if (organizationId is null)
                {
                    throw new TenantViolationException(
                        $"Cannot save {entry.Entity.GetType().Name}: no active organization for this request.");
                }

                tenantOwned.OrganizationId = organizationId.Value;
                return;
            }

            // An explicitly set organization is only allowed when it matches the caller's own,
            // or when a bypass is in effect (registration, seeding, background jobs).
            if (!bypassed && organizationId is not null && tenantOwned.OrganizationId != organizationId.Value)
            {
                Reject(entry, tenantOwned.OrganizationId, organizationId.Value);
            }

            return;
        }

        if (entry.State is EntityState.Modified or EntityState.Deleted && !bypassed && organizationId is not null)
        {
            // Compare against the value loaded from the database, so reassigning OrganizationId
            // in memory cannot be used to move a row into another tenant.
            var original = entry.Property(nameof(ITenantOwned.OrganizationId)).OriginalValue as Guid?;

            if (original is not null && original.Value != organizationId.Value)
            {
                Reject(entry, original.Value, organizationId.Value);
            }

            if (tenantOwned.OrganizationId != organizationId.Value)
            {
                Reject(entry, tenantOwned.OrganizationId, organizationId.Value);
            }
        }
    }

    private static void ConvertHardDeleteToSoftDelete(EntityEntry entry, DateTime now, Guid? userId)
    {
        if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletable softDeletable)
        {
            return;
        }

        entry.State = EntityState.Modified;
        softDeletable.IsDeleted = true;
        softDeletable.DeletedAt = now;
        softDeletable.DeletedBy = userId;
    }

    private void Reject(EntityEntry entry, Guid attempted, Guid current)
    {
        logger.LogWarning(
            "Blocked a cross-tenant write to {EntityType}: row belongs to {AttemptedOrganizationId} but the request is in {CurrentOrganizationId}.",
            entry.Entity.GetType().Name, attempted, current);

        throw new TenantViolationException(
            $"A {entry.Entity.GetType().Name} belonging to another organization cannot be modified.");
    }
}
