using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.DTOs;

namespace EventFlow.Application.Interfaces
{
    public interface IEndpointService
    {
        Task<EndpointCreationOutput> CreateEndpointAsync(CreateEndpointInput input);
        Task<EndpointOutput> GetEndpointAsync(Guid endpointId, Guid projectId, Guid userId);
        Task<IEnumerable<EndpointOutput>> GetEndpointsAsync(Guid projectId, Guid userId);
        Task<EndpointOutput> UpdateEndpointAsync(UpdateEndpointInput input);
        Task DeleteEndpointAsync(Guid endpointId, Guid projectId, Guid userId);
        Task ActivateEndpointAsync(Guid endpointId, Guid projectId, Guid userId);
        Task DeactivateEndpointAsync(Guid endpointId, Guid projectId, Guid userId);
    }
}
