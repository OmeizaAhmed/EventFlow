using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class EntityRelationshipTests
{
    private static EventFlowDbContext CreateContext() => new(
        new DbContextOptionsBuilder<EventFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=model_test")
            .Options);

    [Fact]
    public void DomainRelationships_HaveNoShadowForeignKeysOrUnpairedNavigations()
    {
        using var context = CreateContext();
        var entities = context.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(Project).Namespace).ToArray();

        Assert.Equal(14, entities.Sum(e => e.GetForeignKeys().Count()));
        foreach (var entity in entities)
        {
            Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
            foreach (var relationship in entity.GetForeignKeys())
            {
                Assert.NotNull(relationship.PrincipalToDependent);
                if (relationship.PrincipalEntityType.ClrType != typeof(ApplicationUser))
                    Assert.NotNull(relationship.DependentToPrincipal);
            }
        }

        var token = context.Model.FindEntityType(typeof(RefreshToken))!;
        Assert.Equal(typeof(DomainUser), Assert.Single(token.GetForeignKeys()).PrincipalEntityType.ClrType);
        var user = context.Model.FindEntityType(typeof(DomainUser))!;
        var identity = Assert.Single(user.GetForeignKeys());
        Assert.True(identity.IsUnique);
        Assert.Equal(nameof(DomainUser.AuthId), Assert.Single(identity.Properties).Name);

        var membership = context.Model.FindEntityType(typeof(ProjectMembership))!;
        foreach (var name in new[] { nameof(ProjectMembership.InvitedByUser), nameof(ProjectMembership.LastModifiedByUser) })
        {
            var relationship = membership.FindNavigation(name)!.ForeignKey;
            Assert.False(relationship.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        }
    }

    [Fact]
    public void TrackingGraph_FixesUpReferencesAndReadOnlyCollections()
    {
        using var context = CreateContext();
        var user = DomainUser.Create("owner@example.com", "Test", "Owner", Guid.NewGuid());
        var creation = Project.Create("Test", user.UserId);
        var project = creation.Project;
        var endpoint = project.RegisterEndpoint("https://example.com/webhook", new string('a', 64), "Test");
        var key = project.GenerateApiKey("hashed-key", "prefix", false);
        var subscription = endpoint.Subscribe("order.created");
        var evt = Event.Create(project.ProjectId, "order.created", "{}");
        var delivery = Delivery.Create(evt.EventId, endpoint.EndpointId);
        delivery.RecordAttempt(200, null, true);
        var token = RefreshToken.Create(user.UserId, "token", DateTime.UtcNow.AddDays(1));

        context.AddRange(user, project, creation.Membership, evt, delivery, token);
        context.ChangeTracker.DetectChanges();

        Assert.Same(project, endpoint.Project);
        Assert.Same(project, key.Project);
        Assert.Same(project, evt.Project);
        Assert.Contains(evt, project.Events);
        Assert.Same(evt, delivery.Event);
        Assert.Same(endpoint, delivery.Endpoint);
        Assert.Contains(delivery, evt.Deliveries);
        Assert.Contains(delivery, endpoint.Deliveries);
        Assert.Same(delivery, Assert.Single(delivery.Attempts).Delivery);
        Assert.Equal(delivery.DeliveryId, Assert.Single(delivery.Attempts).DeliveryId);
        Assert.Same(endpoint, subscription.Endpoint);
        Assert.Same(project, subscription.Project);
        Assert.Same(user, token.User);
        Assert.Contains(token, user.RefreshTokens);
        Assert.Same(user, creation.Membership.InvitedByUser);
        Assert.Same(user, creation.Membership.LastModifiedByUser);
        Assert.Contains(creation.Membership, user.InvitedMemberships);
        Assert.Contains(creation.Membership, user.LastModifiedMemberships);
    }

    [Fact]
    public void NavigationQueries_TranslateToPostgresJoins()
    {
        using var context = CreateContext();
        var sql = context.Deliveries
            .Include(d => d.Event).ThenInclude(e => e.Project)
            .Include(d => d.Endpoint)
            .Include(d => d.Attempts)
            .ToQueryString();

        Assert.Contains("JOIN \"Events\"", sql);
        Assert.Contains("JOIN \"Projects\"", sql);
        Assert.Contains("JOIN \"WebhookEndpoints\"", sql);
        Assert.Contains("JOIN \"DeliveryAttempts\"", sql);
        Assert.DoesNotContain("EndpointId1", sql);
        Assert.DoesNotContain("ProjectId1", sql);
    }
}
