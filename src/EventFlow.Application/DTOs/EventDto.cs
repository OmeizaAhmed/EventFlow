using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace EventFlow.Application.DTOs;
public record EventRequest(string EventType, object Payload);
public record EventInput(Guid ProjectId, string EventType, object Payload, string IdempotencyKey);

public sealed class EventHistoryQuery
{
    [StringLength(100)]
    public string? EventType { get; set; }

    [Range(1, 1000000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public record EventPage(IReadOnlyList<EventSummary> Items, int TotalCount, int Page, int PageSize);
public record EventSummary(Guid EventId, Guid ProjectId, string EventType, DateTime CreatedAt, int DeliveryCount);
public record EventDetail(Guid EventId, Guid ProjectId, string EventType, DateTime CreatedAt, int DeliveryCount,
    string Payload);
