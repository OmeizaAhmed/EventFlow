using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Repositories;

public sealed class DeliveryInspectionRepository(EventFlowDbContext context) : IDeliveryInspectionRepository
{
    public async Task<DeliveryPage<DeliverySummary>> ListAsync(Guid projectId, DeliveryQuery query, CancellationToken ct)
    {
        var rows = context.Deliveries.AsNoTracking().Where(d => d.Event.ProjectId == projectId);
        if (query.Status is { } status) rows = rows.Where(d => d.Status == status);
        if (query.EventId is { } eventId) rows = rows.Where(d => d.EventId == eventId);
        if (query.EndpointId is { } endpointId) rows = rows.Where(d => d.EndpointId == endpointId);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.DeliveryId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(d => new DeliverySummary(d.DeliveryId, d.EventId, d.EndpointId, d.ReplayOfDeliveryId,
                d.Event.EventType, d.Status.ToString(), d.CreatedAt, d.NextRetryAt, d.Attempts.Count))
            .ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public Task<Delivery?> GetAsync(Guid projectId, Guid deliveryId, CancellationToken ct) =>
        context.Deliveries.AsNoTracking().Include(d => d.Event).Include(d => d.Endpoint)
            .SingleOrDefaultAsync(d => d.DeliveryId == deliveryId && d.Event.ProjectId == projectId, ct);

    public async Task<DeliveryPage<DeliveryAttemptOutput>> AttemptsAsync(Guid deliveryId, int page, int pageSize, CancellationToken ct)
    {
        var rows = context.DeliveryAttempts.AsNoTracking().Where(a => a.DeliveryId == deliveryId);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(a => a.AttemptNumber).ThenBy(a => a.DeliveryAttemptId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new DeliveryAttemptOutput(a.DeliveryAttemptId, a.AttemptNumber, a.HttpStatusCode,
                a.ErrorMessage, a.Succeeded, a.AttemptedAt, a.ResponseBody, a.DurationMilliseconds)).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task AddAsync(Delivery delivery, CancellationToken ct)
    {
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync(ct);
    }
}
