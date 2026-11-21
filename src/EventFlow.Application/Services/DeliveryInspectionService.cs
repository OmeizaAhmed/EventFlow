using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace EventFlow.Application.Services;

public sealed class DeliveryInspectionService(IDeliveryInspectionRepository deliveries,
    IProjectRepository projects, ILogger<DeliveryInspectionService> logger)
{
    private async Task<ProjectMembership> AuthorizeAsync(Guid projectId, Guid userId, bool replay = false)
    {
        var member = await projects.GetMembershipAsync(userId, projectId);
        if (member is null || !member.IsActive)
            throw new NotFoundException("Project", projectId);
        if (replay && !member.CanManageEvents())
            throw new ForbiddenException("User does not have permission to replay deliveries.");
        return member;
    }

    public async Task<DeliveryPage<DeliverySummary>> ListAsync(Guid projectId, Guid userId, DeliveryQuery query, CancellationToken ct)
    {
        await AuthorizeAsync(projectId, userId);
        return await deliveries.ListAsync(projectId, query, ct);
    }

    public async Task<DeliveryDetail> GetAsync(Guid projectId, Guid deliveryId, Guid userId, CancellationToken ct)
    {
        await AuthorizeAsync(projectId, userId);
        var delivery = await RequireDeliveryAsync(projectId, deliveryId, ct);
        // Counts are read separately so a large attempt history is never materialized.
        var attempts = await deliveries.AttemptsAsync(deliveryId, 1, 1, ct);
        return new(Summary(delivery, attempts.TotalCount), delivery.Event.Payload, delivery.Endpoint.Url);
    }

    public async Task<DeliveryPage<DeliveryAttemptOutput>> AttemptsAsync(Guid projectId, Guid deliveryId,
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(projectId, userId);
        await RequireDeliveryAsync(projectId, deliveryId, ct);
        return await deliveries.AttemptsAsync(deliveryId, page, pageSize, ct);
    }

    public async Task<DeliverySummary> ReplayAsync(Guid projectId, Guid deliveryId, Guid userId, CancellationToken ct)
    {
        var member = await AuthorizeAsync(projectId, userId, replay: true);
        var source = await RequireDeliveryAsync(projectId, deliveryId, ct);
        if (!member.Project.IsActive || !source.Endpoint.IsActive)
            throw new ConflictException("The project and endpoint must be active to replay a delivery.");
        var replay = source.Replay();
        await deliveries.AddAsync(replay, ct);
        logger.LogInformation("Delivery {DeliveryId} replayed as {ReplayId} in project {ProjectId} by user {UserId}",
            deliveryId, replay.DeliveryId, projectId, userId);
        return new(replay.DeliveryId, replay.EventId, replay.EndpointId, deliveryId,
            source.Event.EventType, replay.Status.ToString(), replay.CreatedAt, null, 0);
    }

    private async Task<Delivery> RequireDeliveryAsync(Guid projectId, Guid deliveryId, CancellationToken ct)
        => await deliveries.GetAsync(projectId, deliveryId, ct) ?? throw new NotFoundException("Delivery", deliveryId);

    private static DeliverySummary Summary(Delivery d, int count) => new(d.DeliveryId, d.EventId, d.EndpointId,
        d.ReplayOfDeliveryId, d.Event.EventType, d.Status.ToString(), d.CreatedAt, d.NextRetryAt, count);
}
