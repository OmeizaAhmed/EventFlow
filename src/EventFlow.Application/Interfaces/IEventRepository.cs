using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Domain.Entities;
namespace EventFlow.Application.Interfaces
{
    public interface IEventRepository
    {
        Event AddEvent(Event evt);
        Task<Event?> GetEventByIdAsync(Guid eventId);
        Task<IEnumerable<Event>> GetEventsByProjectIdAsync(Guid projectId);
        Task<Event?> GetEventByIdempotencyKeyAsync(Guid projectId, string idempotencyKey);
        Task<Event> UpdateEventAsync(Event evt);
        Task DeleteEventAsync(Guid eventId);

        // Delivery-related methods
        Delivery AddDelivery(Delivery delivery);
        Task<Delivery?> GetDeliveryByIdAsync(Guid deliveryId);
        Task<IEnumerable<Delivery>> GetDeliveriesByEventIdAsync(Guid eventId);
        Task<Delivery> UpdateDeliveryAsync(Delivery delivery);
        Task DeleteDeliveryAsync(Guid deliveryId);

        Task SaveChangesAsync();
    }
}
