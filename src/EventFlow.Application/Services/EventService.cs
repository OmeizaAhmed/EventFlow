using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Application.DTOs;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace EventFlow.Application.Services;
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IEndpointRepository _endpointRepository;
    private readonly ILogger<EventService> _logger;

    public EventService(IEventRepository eventRepository, IEndpointRepository endpointRepository, ILogger<EventService>? logger = null)
    {
        _eventRepository = eventRepository;
        _endpointRepository = endpointRepository;
        _logger = logger ?? NullLogger<EventService>.Instance;
    }

    public async Task<Event> CreateEventAsync(EventInput eventInput)
    {
        // Check for idempotency
        var existingEvent = await _eventRepository.GetEventByIdempotencyKeyAsync(eventInput.ProjectId, eventInput.IdempotencyKey);
        if (existingEvent != null)
        {
            _logger.LogInformation("Duplicate event publication resolved to event {EventId} in project {ProjectId}", existingEvent.EventId, eventInput.ProjectId);
            return existingEvent;
        }
        var endpoints = await _endpointRepository.GetEndpointsByEventTypeAsync(eventInput.ProjectId, eventInput.EventType);
        // Create the event
        var newEvent = Event.Create(eventInput.ProjectId, eventInput.EventType, JsonSerializer.Serialize(eventInput.Payload), eventInput.IdempotencyKey);

        // create deliveries for each endpoint
        foreach (var endpoint in endpoints)
        {
            var delivery = Delivery.Create(newEvent.EventId, endpoint.EndpointId);
            _eventRepository.AddDelivery(delivery);
        }

        _eventRepository.AddEvent(newEvent);
        await _eventRepository.SaveChangesAsync();
        _logger.LogInformation("Published event {EventId} in project {ProjectId} with {DeliveryCount} queued deliveries", newEvent.EventId, eventInput.ProjectId, endpoints.Count());
        return newEvent;
    }
}
