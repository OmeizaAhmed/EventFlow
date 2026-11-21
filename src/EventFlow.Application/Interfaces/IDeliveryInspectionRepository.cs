using EventFlow.Application.DTOs;
using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces;

public interface IDeliveryInspectionRepository
{
    Task<DeliveryPage<DeliverySummary>> ListAsync(Guid projectId, DeliveryQuery query, CancellationToken ct);
    Task<Delivery?> GetAsync(Guid projectId, Guid deliveryId, CancellationToken ct);
    Task<DeliveryPage<DeliveryAttemptOutput>> AttemptsAsync(Guid deliveryId, int page, int pageSize, CancellationToken ct);
    Task AddAsync(Delivery delivery, CancellationToken ct);
}
