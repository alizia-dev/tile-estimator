using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Application.Abstractions;
using TileEstimator.Domain.Organizations;
using TileEstimator.Infrastructure.Persistence;

namespace TileEstimator.Infrastructure.Services;

/// <summary>
/// Allocates per-organization document numbers such as CUST-1001, PRJ-1001 and EST-1001.
/// <para>
/// Concurrency: the row is read with an UPDLOCK so two simultaneous requests queue rather than
/// both reading the same value. Should the counter still land on a number that exists (for
/// example after a restore), the unique index on the document table rejects the insert, and the
/// caller retries rather than silently duplicating a number.
/// </para>
/// </summary>
public sealed class NumberSequenceService(ApplicationDbContext db) : INumberSequenceService
{
    public const string Customer = "Customer";
    public const string Project = "Project";
    public const string Estimate = "Estimate";
    public const string Quote = "Quote";
    public const string ChangeOrder = "ChangeOrder";

    public async Task<string> NextAsync(Guid organizationId, string entityType, string prefix,
        CancellationToken cancellationToken)
    {
        var sequence = await LoadWithLockAsync(organizationId, entityType, cancellationToken);

        if (sequence is null)
        {
            sequence = NumberSequence.Create(organizationId, entityType);
            db.NumberSequences.Add(sequence);
        }

        var value = sequence.Take();
        await db.SaveChangesAsync(cancellationToken);

        return $"{prefix}-{value.ToString(CultureInfo.InvariantCulture)}";
    }

    private async Task<NumberSequence?> LoadWithLockAsync(Guid organizationId, string entityType,
        CancellationToken cancellationToken)
    {
        // The relational lock hint only applies to a real database; the in-memory provider used
        // by some tests does not support raw SQL, so fall back to a tracked query there.
        if (!db.Database.IsRelational())
        {
            return await db.NumberSequences
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.EntityType == entityType,
                    cancellationToken);
        }

        return await db.NumberSequences
            .FromSqlRaw(
                "SELECT * FROM NumberSequences WITH (UPDLOCK, HOLDLOCK) WHERE OrganizationId = {0} AND EntityType = {1}",
                organizationId, entityType)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
