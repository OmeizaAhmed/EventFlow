using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class EventSubscriptionMappingTests
{
    [Fact]
    public void Subscription_NavigationsUseExplicitForeignKeysWithoutShadowRelationships()
    {
        var options = new DbContextOptionsBuilder<EventFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=model_test")
            .Options;
        using var context = new EventFlowDbContext(options);
        var subscription = context.Model.FindEntityType(typeof(EventSubscription))!;

        Assert.Equal(2, subscription.GetForeignKeys().Count());
        Assert.DoesNotContain(subscription.GetProperties(), property => property.IsShadowProperty());

        var endpoint = subscription.FindNavigation(nameof(EventSubscription.Endpoint))!;
        Assert.Equal(nameof(EventSubscription.EndpointId), Assert.Single(endpoint.ForeignKey.Properties).Name);
        Assert.Equal(nameof(WebhookEndpoint.Subscriptions), endpoint.Inverse!.Name);

        var project = subscription.FindNavigation(nameof(EventSubscription.Project))!;
        Assert.Equal(nameof(EventSubscription.ProjectId), Assert.Single(project.ForeignKey.Properties).Name);
        Assert.Equal(nameof(Project.Subscriptions), project.Inverse!.Name);
    }
}
