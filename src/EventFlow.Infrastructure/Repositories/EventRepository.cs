using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Repositories;

public class EventRepository : IEventRepository
{
    private readonly EventFlowDbContext _context;

    public EventRepository(EventFlowDbContext context)
    {
        _context = context;
    }
    public Event AddEvent(Event evt)
    {
        _context.Events.Add(evt);
        return evt;
    }

    public async Task<Event?> GetEventByIdempotencyKeyAsync(Guid projectId, string idempotencyKey)
    {
        return await _context.Events
            .FirstOrDefaultAsync(e => e.ProjectId == projectId && e.IdempotencyKey == idempotencyKey);
    }

    public async Task<Event?> GetEventByIdAsync(Guid eventId)
    {
        return await _context.Events.FindAsync(eventId);
    }

    public async Task<IEnumerable<Event>> GetEventsByProjectIdAsync(Guid projectId)
    {
        return await _context.Events
            .Where(e => e.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<Event> UpdateEventAsync(Event evt)
    {
        _context.Events.Update(evt);
        return evt;
    }

    public async Task DeleteEventAsync(Guid eventId)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt != null)
        {
            _context.Events.Remove(evt);
        }
    }

    public Delivery AddDelivery(Delivery delivery)
    {
        _context.Deliveries.Add(delivery);
        return delivery;
    }

    public async Task<Delivery?> GetDeliveryByIdAsync(Guid deliveryId)
    {
        return await _context.Deliveries.FindAsync(deliveryId);
    }

    public async Task<IEnumerable<Delivery>> GetDeliveriesByEventIdAsync(Guid eventId)
    {
        return await _context.Deliveries
            .Where(d => d.EventId == eventId)
            .ToListAsync();
    }

    public async Task<Delivery> UpdateDeliveryAsync(Delivery delivery)
    {
        _context.Deliveries.Update(delivery);
        return delivery;
    }

    public async Task DeleteDeliveryAsync(Guid deliveryId)
    {
        var delivery = await _context.Deliveries.FindAsync(deliveryId);
        if (delivery != null)
        {
            _context.Deliveries.Remove(delivery);
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
