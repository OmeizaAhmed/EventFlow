using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EventFlow.Infrastructure.Repositories
{
    public class EndpointRepository : IEndpointRepository
    {
        private readonly EventFlowDbContext _dbContext;

        public EndpointRepository(EventFlowDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<WebhookEndpoint> CreateEndpointAsync(WebhookEndpoint endpoint)
        {
            _dbContext.WebhookEndpoints.Add(endpoint);
            await _dbContext.SaveChangesAsync();
            return endpoint;
        }
        public async Task<WebhookEndpoint?> GetEndpointByIdAsync(Guid endpointId)
        {
            return await _dbContext.WebhookEndpoints.FindAsync(endpointId);
        }
        public async Task<IEnumerable<WebhookEndpoint>> GetAllEndpointsAsync(Guid projectId)
        {
            return await _dbContext.WebhookEndpoints
                .Where(e => e.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<IEnumerable<WebhookEndpoint>> GetEndpointsByEventTypeAsync(Guid projectId, string eventType, bool onlyActive = true)
        {
               var subscriptions = await _dbContext.EventSubscriptions
                .AsNoTracking()
                .Include(s => s.Endpoint)
                .Where(s => s.EventType == eventType && s.Endpoint.ProjectId == projectId && (!onlyActive || s.Endpoint.IsActive))
                .ToListAsync();

            return subscriptions.Select(s => s.Endpoint);
        }

        public async Task<WebhookEndpoint> UpdateEndpointAsync(WebhookEndpoint endpoint)
        {
            _dbContext.WebhookEndpoints.Update(endpoint);
            await _dbContext.SaveChangesAsync();
            return endpoint;
        }
        public async Task DeleteEndpointAsync(Guid endpointId)
        {
            var endpoint = await _dbContext.WebhookEndpoints.FindAsync(endpointId);
            if (endpoint != null)
            {
                _dbContext.WebhookEndpoints.Remove(endpoint);
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
        // Additional methods for managing event subscriptions
        public async Task<EventSubscription> CreateSubscriptionAsync(EventSubscription subscription)
        {
            _dbContext.EventSubscriptions.Add(subscription);
            await _dbContext.SaveChangesAsync();
            return subscription;
        }
        public async Task<EventSubscription?> GetSubscriptionByIdAsync(Guid subscriptionId)
        {
            return await _dbContext.EventSubscriptions.FindAsync(subscriptionId);
        }
        public async Task<IEnumerable<EventSubscription>> GetAllSubscriptionsAsync(Guid endpointId)
        {
            return await _dbContext.EventSubscriptions
                .Where(s => s.EndpointId == endpointId)
                .ToListAsync();
        }
        public async Task<EventSubscription> UpdateSubscriptionAsync(EventSubscription subscription)
        {
            _dbContext.EventSubscriptions.Update(subscription);
            await _dbContext.SaveChangesAsync();
            return subscription;
        }
        public async Task DeleteSubscriptionAsync(Guid subscriptionId)
        {
            var subscription = await _dbContext.EventSubscriptions.FindAsync(subscriptionId);
            if (subscription != null)
            {
                _dbContext.EventSubscriptions.Remove(subscription);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}


