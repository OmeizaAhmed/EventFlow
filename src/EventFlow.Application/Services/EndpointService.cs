using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


namespace EventFlow.Application.Services
{
    
    public class EndpointService : IEndpointService
    {
        private readonly IEndpointRepository _endpointRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ISigningKeyService _signingKeyService;
        private readonly ILogger<EndpointService> _logger;

        public EndpointService(IEndpointRepository endpointRepository, ISigningKeyService signingKeyService, IProjectRepository projectRepository, ILogger<EndpointService>? logger = null)
        {
            _endpointRepository = endpointRepository;
            _signingKeyService = signingKeyService;
            _projectRepository = projectRepository; 
            _logger = logger ?? NullLogger<EndpointService>.Instance;
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
            _logger.LogInformation("Endpoint {EndpointId} created in project {ProjectId}", endpoint.EndpointId, endpoint.ProjectId);

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
            _logger.LogInformation("Endpoint {EndpointId} updated in project {ProjectId}", endpoint.EndpointId, endpoint.ProjectId);
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
            _logger.LogInformation("Endpoint {EndpointId} deleted from project {ProjectId}", endpointId, projectId);
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
            _logger.LogInformation("Endpoint {EndpointId} activated in project {ProjectId}", endpointId, projectId);
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
            _logger.LogInformation("Endpoint {EndpointId} deactivated in project {ProjectId}", endpointId, projectId);
        }

        // Additional methods for managing event subscriptions can be added here, following a similar pattern of permission checks and repository interactions.

        public async Task<EventSubscriptionOutput> CreateSubscriptionAsync(CreateEventSubscriptionInput input)
        {
            var membership = await _projectRepository.GetMembershipAsync(input.UserId, input.ProjectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to create a subscription for this endpoint.");
            }
            var endpoint = await _endpointRepository.GetEndpointByIdAsync(input.EndpointId);
            if (endpoint == null || endpoint.ProjectId != input.ProjectId)
            {
                throw new NotFoundException("Endpoint", input.EndpointId);
            }
            var subscription = endpoint.Subscribe(input.EventType);
            await _endpointRepository.CreateSubscriptionAsync(subscription);
            _logger.LogInformation("Subscription {SubscriptionId} created for endpoint {EndpointId} in project {ProjectId}", subscription.EventSubscriptionId, endpoint.EndpointId, input.ProjectId);
            return new EventSubscriptionOutput(subscription.EventSubscriptionId, subscription.ProjectId, subscription.EndpointId, subscription.EventType, subscription.CreatedAt);
        }

        public async Task<EventSubscriptionOutput> GetSubscriptionAsync(Guid subscriptionId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to view this subscription.");
            }
            var subscription = await _endpointRepository.GetSubscriptionByIdAsync(subscriptionId);
            if (subscription == null || subscription.ProjectId != projectId)
            {
                throw new NotFoundException("Subscription", subscriptionId);
            }
            return new EventSubscriptionOutput(subscription.EventSubscriptionId, subscription.ProjectId, subscription.EndpointId, subscription.EventType, subscription.CreatedAt);
        }

        public async Task<IEnumerable<EventSubscriptionOutput>> GetSubscriptionsAsync(Guid endpointId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to view subscriptions for this endpoint.");
            }
            var subscriptions = await _endpointRepository.GetAllSubscriptionsAsync(endpointId);
            return subscriptions.Select(subscription => new EventSubscriptionOutput(subscription.EventSubscriptionId, subscription.ProjectId, subscription.EndpointId, subscription.EventType, subscription.CreatedAt));
        }

        public async Task<EventSubscriptionOutput> UpdateSubscriptionAsync(UpdateEventSubscriptionInput input)
        {
            var membership = await _projectRepository.GetMembershipAsync(input.UserId, input.ProjectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to update this subscription.");
            }
            var subscription = await _endpointRepository.GetSubscriptionByIdAsync(input.SubscriptionId);
            if (subscription == null || subscription.ProjectId != input.ProjectId)
            {
                throw new NotFoundException("Subscription", input.SubscriptionId);
            }
            subscription.UpdateEventType(input.EventType);
            await _endpointRepository.UpdateSubscriptionAsync(subscription);
            _logger.LogInformation("Subscription {SubscriptionId} updated in project {ProjectId}", subscription.EventSubscriptionId, input.ProjectId);
            return new EventSubscriptionOutput(subscription.EventSubscriptionId, subscription.ProjectId, subscription.EndpointId, subscription.EventType, subscription.CreatedAt);
        }

        public async Task DeleteSubscriptionAsync(Guid subscriptionId, Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null || !membership.CanManageEndpoints())
            {
                throw new ForbiddenException("User does not have permission to delete this subscription.");
            }
            var subscription = await _endpointRepository.GetSubscriptionByIdAsync(subscriptionId);
            if (subscription == null || subscription.ProjectId != projectId)
            {
                throw new NotFoundException("Subscription", subscriptionId);
            }
            await _endpointRepository.DeleteSubscriptionAsync(subscriptionId);
            _logger.LogInformation("Subscription {SubscriptionId} deleted from project {ProjectId}", subscriptionId, projectId);
        }
            
    }
}
