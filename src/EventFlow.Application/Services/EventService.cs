using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Application.DTOs;
using System.Text.Json.Nodes;
using System.Text.Json;
namespace EventFlow.Application.Services;
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IEndpointRepository _endpointRepository;

    public EventService(IEventRepository eventRepository, IEndpointRepository endpointRepository)
    {
        _eventRepository = eventRepository;
        _endpointRepository = endpointRepository;
    }

    public async Task<Event> CreateEventAsync(EventInput eventInput)
    {
        // Check for idempotency
        var existingEvent = await _eventRepository.GetEventByIdempotencyKeyAsync(eventInput.ProjectId, eventInput.IdempotencyKey);
        if (existingEvent != null)
        {
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
        return newEvent;
    }
}
