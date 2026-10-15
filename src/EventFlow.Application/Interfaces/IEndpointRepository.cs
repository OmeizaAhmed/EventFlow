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
    }
}
