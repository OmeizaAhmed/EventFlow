using EventFlow.Domain.ValueObject;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs
{
    public record EndpointRequest(string Url, string Description);
    public record CreateEndpointInput(
        Guid ProjectId,
        Guid UserId,
        string Url,
        string Description
    );
    public record UpdateEndpointInput(
        Guid EndpointId,
        Guid ProjectId,
        Guid UserId,
        string Url,
        string Description
    );

    public record EndpointCreationOutput(
        Guid EndpointId,
        Guid ProjectId,
        string Url,
        string SigningSecret,
        string Description,
        bool IsActive,
        DateTime CreatedAt
    );

    public record EndpointOutput(
        Guid EndpointId,
        Guid ProjectId,
        string Url,
        string Description,
        bool IsActive,
        DateTime CreatedAt
    );

    // Additional DTOs for event subscriptions
    public record EventSubscriptionRequest(
       string EventType
    );

    public record CreateEventSubscriptionInput(
        Guid ProjectId,
        Guid EndpointId,
        Guid UserId,
        string EventType
    );

    public record UpdateEventSubscriptionInput(
        Guid SubscriptionId,
        Guid ProjectId,
        Guid EndpointId,
        Guid UserId,
        string EventType
    );

    public record EventSubscriptionOutput(
        Guid SubscriptionId,
        Guid ProjectId,
        Guid EndpointId,
        string EventType,
        DateTime CreatedAt
    );
}
