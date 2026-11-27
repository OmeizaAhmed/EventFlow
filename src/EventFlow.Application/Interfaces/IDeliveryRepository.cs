using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces;

/// <summary>
/// A scoped unit of work for one delivery attempt. Use a fresh scope for each claim;
/// the claimed delivery and its complete attempt history remain tracked until saved.
/// </summary>
public interface IDeliveryRepository
{
    Task<Delivery?> ClaimAsync(
        Guid deliveryId,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    Task<Delivery?> ClaimNextAsync(
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically saves the attempt and delivery state only while the supplied claim
    /// is still owned. A false result means another worker replaced the claim.
    /// </summary>
    Task<bool> SaveClaimedAsync(
        Delivery delivery,
        Guid leaseToken,
        CancellationToken ct = default);
}
