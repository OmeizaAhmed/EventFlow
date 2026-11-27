using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Repositories;

/// <summary>
/// Owns one delivery attempt per DI scope. Claiming commits before the caller sends
/// HTTP; the short completion transaction never spans network delivery.
/// </summary>
public sealed class DeliveryRepository(EventFlowDbContext context) : IDeliveryRepository
{
    private const int CandidateBatchSize = 16;

    public async Task<Delivery?> ClaimNextAsync(
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var candidateIds = await ListDueIdsAsync(utcNow, CandidateBatchSize, ct);

        foreach (var deliveryId in candidateIds)
        {
            var delivery = await ClaimAsync(deliveryId, utcNow, leaseDuration, ct);
            if (delivery is not null)
                return delivery;
        }

        return null;
    }

    public Task<List<Guid>> ListDueIdsAsync(DateTime utcNow, int limit, CancellationToken ct) =>
        EligibleDeliveries(utcNow)
            .OrderBy(d => d.CreatedAt)
            .ThenBy(d => d.DeliveryId)
            .Select(d => d.DeliveryId)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<Delivery?> ClaimAsync(Guid deliveryId, DateTime utcNow, TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var leaseToken = Guid.NewGuid();
        var leaseExpiresAt = utcNow.Add(leaseDuration);
        // The conditional UPDATE lets only one job claim an eligible delivery.
        var claimed = await EligibleDeliveries(utcNow)
            .Where(d => d.DeliveryId == deliveryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Status, DeliveryStatus.Processing)
                .SetProperty(d => d.LeaseToken, (Guid?)leaseToken)
                .SetProperty(d => d.LeaseExpiresAt, (DateTime?)leaseExpiresAt), ct);

        if (claimed == 0)
            return null;

        return await context.Deliveries
            .AsTracking()
            .Include(d => d.Event).ThenInclude(e => e.Project)
            .Include(d => d.Endpoint)
            .Include(d => d.Attempts)
            .SingleOrDefaultAsync(d => d.DeliveryId == deliveryId && d.LeaseToken == leaseToken, ct);
    }

    public async Task<bool> SaveClaimedAsync(
        Delivery delivery,
        Guid leaseToken,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(delivery);
        var entry = context.Entry(delivery);
        if (entry.State == EntityState.Detached ||
            entry.Property(d => d.LeaseToken).OriginalValue != leaseToken)
        {
            throw new InvalidOperationException("The delivery must be saved in the scope that claimed it.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        // This conditional no-op UPDATE fences the claimant and holds the row lock
        // until SaveChanges commits. Without it, a stale attempt insert could hit
        // its unique index before EF runs the optimistic-concurrency UPDATE.
        var stillOwned = await context.Deliveries
            .Where(d => d.DeliveryId == delivery.DeliveryId &&
                d.Status == DeliveryStatus.Processing && d.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.LeaseToken, (Guid?)leaseToken), ct);

        if (stillOwned == 0)
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            return false;
        }

        try
        {
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private IQueryable<Delivery> EligibleDeliveries(DateTime utcNow) =>
        context.Deliveries.Where(d =>
            (d.Status == DeliveryStatus.Queued ||
             (d.Status == DeliveryStatus.RetryScheduled && d.NextRetryAt <= utcNow) ||
             (d.Status == DeliveryStatus.Processing && d.LeaseExpiresAt <= utcNow)) &&
            // Keep the lease/status predicates directly on the UPDATE target.
            // Related eligibility uses EXISTS instead of adding outer query joins.
            context.WebhookEndpoints.Any(e => e.EndpointId == d.EndpointId && e.IsActive) &&
            context.Events.Any(e => e.EventId == d.EventId &&
                context.Projects.Any(p => p.ProjectId == e.ProjectId && p.IsActive)));
}
