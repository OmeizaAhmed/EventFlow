using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces
{
    public interface IEndpointRepository
    {
        Task<WebhookEndpoint> CreateEndpointAsync(WebhookEndpoint endpoint);
        Task<WebhookEndpoint?> GetEndpointByIdAsync(Guid endpointId);
        Task<IEnumerable<WebhookEndpoint>> GetAllEndpointsAsync(Guid projectId);
        Task<WebhookEndpoint> UpdateEndpointAsync(WebhookEndpoint endpoint);
        Task DeleteEndpointAsync(Guid endpointId);
        Task SaveChangesAsync();

        // Additional methods for managing event subscriptions
        Task<EventSubscription> CreateSubscriptionAsync(EventSubscription subscription);
        Task<EventSubscription?> GetSubscriptionByIdAsync(Guid subscriptionId);
        Task<IEnumerable<EventSubscription>> GetAllSubscriptionsAsync(Guid endpointId);
        Task<EventSubscription> UpdateSubscriptionAsync(EventSubscription subscription);
        Task DeleteSubscriptionAsync(Guid subscriptionId);
    }
}
