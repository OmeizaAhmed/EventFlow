using System.ComponentModel.DataAnnotations;
using EventFlow.Domain.Enums;

namespace EventFlow.Application.DTOs;

public class DeliveryQuery
{
    [EnumDataType(typeof(DeliveryStatus))]
    public DeliveryStatus? Status { get; set; }
    public Guid? EventId { get; set; }
    public Guid? EndpointId { get; set; }
    [Range(1, 1000000)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public record DeliveryPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public record DeliverySummary(Guid DeliveryId, Guid EventId, Guid EndpointId,
    Guid? ReplayOfDeliveryId, string EventType, string Status, DateTime CreatedAt,
    DateTime? NextRetryAt, int AttemptCount);
public record DeliveryDetail(DeliverySummary Delivery, string Payload, string EndpointUrl);
public record DeliveryAttemptOutput(Guid DeliveryAttemptId, int AttemptNumber,
    int? HttpStatusCode, string? ErrorMessage, bool Succeeded, DateTime AttemptedAt,
    string? ResponseBody, long DurationMilliseconds);
