using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Exceptions;


namespace EventFlow.Application.Services
{
    
    public class EndpointService : IEndpointService
    {
        private readonly IEndpointRepository _endpointRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ISigningKeyService _signingKeyService;

        public EndpointService(IEndpointRepository endpointRepository, ISigningKeyService signingKeyService, IProjectRepository projectRepository)
        {
            _endpointRepository = endpointRepository;
            _signingKeyService = signingKeyService;
            _projectRepository = projectRepository; 
        }
        public async Task<EndpointCreationOutput> CreateEndpointAsync(CreateEndpointInput input)
        {
            var membership = await _projectRepository.GetMembershipAsync(input.UserId, input.ProjectId);
            if(membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to create an endpoint for this project.");
            }
            var signingSecret = _signingKeyService.GenerateSigningKey();
            var encryptedSecret = _signingKeyService.Encrypt(signingSecret);

            var project = membership.Project;
            var endpoint = project.RegisterEndpoint(input.Url, encryptedSecret, input.Description);
            await _endpointRepository.CreateEndpointAsync(endpoint);

            return new EndpointCreationOutput(
                endpoint.EndpointId,
                endpoint.ProjectId,
                endpoint.Url,
                signingSecret,
                endpoint.Description,
                endpoint.IsActive,
                endpoint.CreatedAt);
        }

        public async Task<EndpointOutput> GetEndpointAsync(Guid endpointId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new ForbiddenException("User does not have permission to view this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(endpointId);
            if (endpoint == null || endpoint.ProjectId != projectId)
            {
                throw new NotFoundException("Endpoint", endpointId);
            }
            return new EndpointOutput(
                endpoint.EndpointId,
                endpoint.ProjectId,
                endpoint.Url,
                endpoint.Description,
                endpoint.IsActive,
                endpoint.CreatedAt);
        }

        public async Task<IEnumerable<EndpointOutput>> GetEndpointsAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new ForbiddenException("User does not have permission to view this endpoint.");
            }
            var endpoints = await _endpointRepository.GetAllEndpointsAsync(projectId);
            return endpoints.Select(endpoint => new EndpointOutput(
                endpoint.EndpointId,
                endpoint.ProjectId,
                endpoint.Url,
                endpoint.Description,
                endpoint.IsActive,
                endpoint.CreatedAt));
        }

        public async Task<EndpointOutput> UpdateEndpointAsync(UpdateEndpointInput input)
        {
            var membership = await _projectRepository.GetMembershipAsync(input.UserId, input.ProjectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to update this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(input.EndpointId);
            if (endpoint == null || endpoint.ProjectId != input.ProjectId)
            {
                throw new NotFoundException("Endpoint", input.EndpointId);
            }
            endpoint.Update(input.Url, input.Description);
            await _endpointRepository.UpdateEndpointAsync(endpoint);
            await _endpointRepository.SaveChangesAsync();
            return new EndpointOutput(
                endpoint.EndpointId,
                endpoint.ProjectId,
                endpoint.Url,
                endpoint.Description,
                endpoint.IsActive,
                endpoint.CreatedAt);
        }

        public async Task DeleteEndpointAsync(Guid endpointId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if(membership == null)
            {
                throw new NotFoundException("ProjectId", projectId);
            }
            if(!membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to delete this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(endpointId);
            if (endpoint == null || endpoint.ProjectId != projectId)
            {
                throw new NotFoundException("Endpoint", endpointId);
            }
            await _endpointRepository.DeleteEndpointAsync(endpointId);
        }

        public async Task ActivateEndpointAsync(Guid endpointId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if(membership == null)
            {
                throw new NotFoundException("ProjectId", projectId);
            }
            if(!membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to activate this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(endpointId);
            if (endpoint == null || endpoint.ProjectId != projectId)
            {
                throw new NotFoundException("Endpoint", endpointId);
            }
            endpoint.Activate();
            await _endpointRepository.UpdateEndpointAsync(endpoint);
        }

        public async Task DeactivateEndpointAsync(Guid endpointId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if(membership == null)
            {
                throw new NotFoundException("ProjectId", projectId);
            }
            if(!membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to deactivate this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(endpointId);
            if (endpoint == null || endpoint.ProjectId != projectId)
            {
                throw new NotFoundException("Endpoint", endpointId);
            }
            endpoint.Deactivate();
            await _endpointRepository.UpdateEndpointAsync(endpoint);
        }
    }
}
