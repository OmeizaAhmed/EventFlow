using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
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
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<EventService> _logger;

    public EventService(IEventRepository eventRepository, IEndpointRepository endpointRepository,
        IProjectRepository projectRepository, ILogger<EventService>? logger = null)
    {
        _eventRepository = eventRepository;
        _endpointRepository = endpointRepository;
        _projectRepository = projectRepository;
        _logger = logger ?? NullLogger<EventService>.Instance;
    }

    public async Task<EventPage> ListAsync(Guid projectId, Guid userId, EventHistoryQuery query, CancellationToken ct)
    {
        await RequireProjectReaderAsync(projectId, userId);
        return await _eventRepository.ListForProjectAsync(projectId, query, ct);
    }

    public async Task<EventDetail> GetAsync(Guid projectId, Guid eventId, Guid userId, CancellationToken ct)
    {
        await RequireProjectReaderAsync(projectId, userId);
        return await _eventRepository.GetForProjectAsync(projectId, eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
    }

    private async Task RequireProjectReaderAsync(Guid projectId, Guid userId)
    {
        var member = await _projectRepository.GetMembershipAsync(userId, projectId);
        if (member?.CanReadProject() != true)
            throw new NotFoundException("Project", projectId);
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
