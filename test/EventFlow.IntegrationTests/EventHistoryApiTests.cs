using System.Net;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests
{
    [Fact]
    public async Task EventHistory_PaginatesFiltersAndExposesDetailsToActiveProjectMembers()
    {
        var owner = await RegisterAndLoginAsync();
        var reader = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var other = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, reader.UserId, ProjectMembershipRole.ReadOnly);
        var deliveryId = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Queued);
        var second = Event.Create(project.ProjectId, "customer.created", "{}");
        var unrelated = Event.Create(other.ProjectId, "order.created", "{}");
        var eventId = Guid.Empty;
        await WithDatabaseAsync(async db =>
        {
            eventId = await db.Deliveries.Where(delivery => delivery.DeliveryId == deliveryId)
                .Select(delivery => delivery.EventId).SingleAsync();
            db.Events.AddRange(second, unrelated);
            await db.SaveChangesAsync();
        });

        var url = $"/api/projects/{project.ProjectId}/events";
        using var listed = await reader.Client.GetAsync($"{url}?pageSize=1");
        await AssertStatusAsync(HttpStatusCode.OK, listed);
        var firstPage = await ReadAsync<EventPage>(listed);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(second.EventId, Assert.Single(firstPage.Items).EventId);
        using var filtered = await reader.Client.GetAsync($"{url}?eventType=order.created");
        await AssertStatusAsync(HttpStatusCode.OK, filtered);
        var match = Assert.Single((await ReadAsync<EventPage>(filtered)).Items);
        Assert.Equal(eventId, match.EventId);
        Assert.Equal(1, match.DeliveryCount);
        Assert.DoesNotContain("payload", await filtered.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var detail = await reader.Client.GetAsync($"{url}/{eventId}");
        await AssertStatusAsync(HttpStatusCode.OK, detail);
        Assert.Equal("{\"orderId\":42}", (await ReadAsync<EventDetail>(detail)).Payload);
        using var otherProject = await owner.Client.GetAsync($"{url}/{unrelated.EventId}");
        await AssertStatusAsync(HttpStatusCode.NotFound, otherProject);
        using var inaccessible = await reader.Client.GetAsync($"/api/projects/{other.ProjectId}/events");
        await AssertStatusAsync(HttpStatusCode.NotFound, inaccessible);
        using var invalidPage = await reader.Client.GetAsync($"{url}?pageSize=101");
        await AssertStatusAsync(HttpStatusCode.BadRequest, invalidPage);

        var key = await CreateKeyAsync(owner.Client, project.ProjectId);
        using var publisher = _factory.CreateApiClient();
        SetApiKey(publisher, "Bearer", key.plainTextKey);
        using var publishOnly = await publisher.GetAsync(url);
        await AssertStatusAsync(HttpStatusCode.Unauthorized, publishOnly);
    }
}
