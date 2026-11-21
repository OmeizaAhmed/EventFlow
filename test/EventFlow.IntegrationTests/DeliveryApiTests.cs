using System.Net;
using System.Net.Http.Json;
using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests
{
    [Fact]
    public async Task Deliveries_PaginationFiltersAndMissingIds_AreProjectScoped()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var other = await CreateProjectAsync(owner.Client);
        var first = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Failed);
        await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Delivered);
        await SeedDeliveryAsync(other.ProjectId, DeliveryStatus.DeadLetter);
        var url = $"/api/projects/{project.ProjectId}/deliveries";
        using var pageOneResponse = await owner.Client.GetAsync(url + "?pageSize=1");
        using var pageTwoResponse = await owner.Client.GetAsync(url + "?pageSize=1&page=2");
        var pageOne = await ReadAsync<DeliveryPage<DeliverySummary>>(pageOneResponse);
        var pageTwo = await ReadAsync<DeliveryPage<DeliverySummary>>(pageTwoResponse);
        Assert.Equal(2, pageOne.TotalCount);
        Assert.NotEqual(Assert.Single(pageOne.Items).DeliveryId, Assert.Single(pageTwo.Items).DeliveryId);
        using var detailResponse = await owner.Client.GetAsync($"{url}/{first}");
        var detail = await ReadAsync<DeliveryDetail>(detailResponse);
        using var filteredResponse = await owner.Client.GetAsync($"{url}?eventId={detail.Delivery.EventId}&endpointId={detail.Delivery.EndpointId}");
        Assert.Equal(first, Assert.Single((await ReadAsync<DeliveryPage<DeliverySummary>>(filteredResponse)).Items).DeliveryId);
        using var emptyResponse = await owner.Client.GetAsync(url + "?page=3&pageSize=1");
        Assert.Empty((await ReadAsync<DeliveryPage<DeliverySummary>>(emptyResponse)).Items);
        using var attemptsResponse = await owner.Client.GetAsync($"{url}/{first}/attempts?page=2&pageSize=1");
        var attempts = await ReadAsync<DeliveryPage<DeliveryAttemptOutput>>(attemptsResponse);
        Assert.Equal(1, attempts.TotalCount);
        Assert.Empty(attempts.Items);
        using var invalidAttempts = await owner.Client.GetAsync($"{url}/{first}/attempts?pageSize=0");
        await AssertStatusAsync(HttpStatusCode.BadRequest, invalidAttempts);
        var missing = $"{url}/{Guid.NewGuid()}";
        using var missingRead = await owner.Client.GetAsync(missing);
        using var missingReplay = await owner.Client.PostAsync(missing + "/replay", null);
        await AssertStatusAsync(HttpStatusCode.NotFound, missingRead);
        await AssertStatusAsync(HttpStatusCode.NotFound, missingReplay);
        // Failed deliveries also get a fresh retry budget.
        using var replay = await owner.Client.PostAsync($"{url}/{first}/replay", null);
        await AssertStatusAsync(HttpStatusCode.Accepted, replay);
    }

    [Fact]
    public async Task Deliveries_InspectFilterAndReplay_PreservesHistoryAndQueuesFreshWork()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var id = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.DeadLetter);
        await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Delivered);
        var url = $"/api/projects/{project.ProjectId}/deliveries";
        using var list = await owner.Client.GetAsync(url + "?status=DeadLetter&pageSize=1");
        await AssertStatusAsync(HttpStatusCode.OK, list);
        var page = await ReadAsync<DeliveryPage<DeliverySummary>>(list);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(id, Assert.Single(page.Items).DeliveryId);
        using var detailResponse = await owner.Client.GetAsync($"{url}/{id}");
        var detail = await ReadAsync<DeliveryDetail>(detailResponse);
        Assert.Equal("{\"orderId\":42}", detail.Payload);
        Assert.Equal(1, detail.Delivery.AttemptCount);
        using var attemptsResponse = await owner.Client.GetAsync($"{url}/{id}/attempts");
        var attempts = await ReadAsync<DeliveryPage<DeliveryAttemptOutput>>(attemptsResponse);
        var attempt = Assert.Single(attempts.Items);
        Assert.Equal(503, attempt.HttpStatusCode);
        Assert.Equal("unavailable", attempt.ResponseBody);
        Assert.Equal(23, attempt.DurationMilliseconds);
        using var replayResponse = await owner.Client.PostAsync($"{url}/{id}/replay", null);
        await AssertStatusAsync(HttpStatusCode.Accepted, replayResponse);
        var replay = await ReadAsync<DeliverySummary>(replayResponse);
        Assert.Equal(id, replay.ReplayOfDeliveryId);
        Assert.Equal("Queued", replay.Status);
        Assert.Equal(0, replay.AttemptCount);
        Assert.NotEqual(id, replay.DeliveryId);
        using var follow = await owner.Client.GetAsync(replayResponse.Headers.Location);
        await AssertStatusAsync(HttpStatusCode.OK, follow);
        await WithDatabaseAsync(async db =>
        {
            var source = await db.Deliveries.Include(d => d.Attempts).SingleAsync(d => d.DeliveryId == id);
            Assert.Equal(DeliveryStatus.DeadLetter, source.Status);
            Assert.Single(source.Attempts);
            var queued = await db.Deliveries.SingleAsync(d => d.DeliveryId == replay.DeliveryId);
            Assert.Equal(source.EventId, queued.EventId);
            Assert.Equal(source.EndpointId, queued.EndpointId);
            Assert.Null(queued.LeaseToken);
            Assert.Null(queued.NextRetryAt);
        });
        // The existing worker repository can claim a replay without any special path.
        await using var scope = _factory.Services.CreateAsyncScope();
        var claimed = await scope.ServiceProvider.GetRequiredService<IDeliveryRepository>()
            .ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        Assert.Equal(replay.DeliveryId, claimed?.DeliveryId);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Admin, HttpStatusCode.Accepted)]
    [InlineData(ProjectMembershipRole.Developer, HttpStatusCode.Accepted)]
    [InlineData(ProjectMembershipRole.ReadOnly, HttpStatusCode.Forbidden)]
    public async Task Deliveries_MembersCanInspect_ReplayRequiresEventPermission(ProjectMembershipRole role, HttpStatusCode expected)
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, role);
        var id = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Delivered);
        var url = $"/api/projects/{project.ProjectId}/deliveries/{id}";
        using var read = await member.Client.GetAsync(url);
        await AssertStatusAsync(HttpStatusCode.OK, read);
        using var replay = await member.Client.PostAsync(url + "/replay", null);
        await AssertStatusAsync(expected, replay);
        await WithDatabaseAsync(async db => Assert.Equal(expected == HttpStatusCode.Accepted ? 2 : 1, await db.Deliveries.CountAsync()));
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("outsider", HttpStatusCode.NotFound)]
    [InlineData("other-project", HttpStatusCode.NotFound)]
    [InlineData("inactive-member", HttpStatusCode.NotFound)]
    public async Task Deliveries_RejectUnauthorizedAndCrossProjectAccess(string scenario, HttpStatusCode expected)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var id = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.DeadLetter);
        using var anonymous = _factory.CreateApiClient();
        var client = owner.Client;
        var projectId = project.ProjectId;
        if (scenario == "anonymous") client = anonymous;
        if (scenario == "outsider") client = (await RegisterAndLoginAsync()).Client;
        if (scenario == "other-project") projectId = (await CreateProjectAsync(owner.Client)).ProjectId;
        if (scenario == "inactive-member")
        {
            var member = await RegisterAndLoginAsync();
            await AddMembershipAsync(projectId, owner.UserId, member.UserId, ProjectMembershipRole.Admin);
            await WithDatabaseAsync(async db =>
            {
                var memberships = await db.ProjectMemberships.Where(m => m.ProjectId == projectId).ToListAsync();
                memberships.Single(m => m.UserId == member.UserId).Deactivate(memberships.Single(m => m.UserId == owner.UserId));
                await db.SaveChangesAsync();
            });
            client = member.Client;
        }
        var url = $"/api/projects/{projectId}/deliveries/{id}";
        using var list = await client.GetAsync($"/api/projects/{projectId}/deliveries");
        await AssertStatusAsync(scenario == "other-project" ? HttpStatusCode.OK : expected, list);
        if (scenario == "other-project")
            Assert.Empty((await ReadAsync<DeliveryPage<DeliverySummary>>(list)).Items);
        foreach (var suffix in new[] { "", "/attempts" })
        {
            using var read = await client.GetAsync(url + suffix);
            await AssertStatusAsync(expected, read);
        }
        using var replay = await client.PostAsync(url + "/replay", null);
        await AssertStatusAsync(expected, replay);
        await WithDatabaseAsync(async db => Assert.Equal(1, await db.Deliveries.CountAsync()));
    }

    [Theory]
    [InlineData(DeliveryStatus.Queued)]
    [InlineData(DeliveryStatus.Processing)]
    [InlineData(DeliveryStatus.RetryScheduled)]
    public async Task Deliveries_ReplayRejectsInFlightWork(DeliveryStatus status)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var id = await SeedDeliveryAsync(project.ProjectId, status);
        using var response = await owner.Client.PostAsync($"/api/projects/{project.ProjectId}/deliveries/{id}/replay", null);
        await AssertStatusAsync(HttpStatusCode.Conflict, response);
        await WithDatabaseAsync(async db => Assert.Equal(1, await db.Deliveries.CountAsync()));
    }

    [Theory]
    [InlineData("project")]
    [InlineData("endpoint")]
    public async Task Deliveries_ReplayRejectsInactiveDestination(string inactive)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var id = await SeedDeliveryAsync(project.ProjectId, DeliveryStatus.Failed);
        await WithDatabaseAsync(async db =>
        {
            if (inactive == "project") (await db.Projects.SingleAsync()).Deactivate();
            else (await db.WebhookEndpoints.SingleAsync()).Deactivate();
            await db.SaveChangesAsync();
        });
        using var response = await owner.Client.PostAsync($"/api/projects/{project.ProjectId}/deliveries/{id}/replay", null);
        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=999")]
    [InlineData("status=unknown")]
    [InlineData("eventId=invalid")]
    public async Task Deliveries_InvalidFilters_ReturnBadRequest(string query)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        using var response = await owner.Client.GetAsync($"/api/projects/{project.ProjectId}/deliveries?{query}");
        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    private async Task<Guid> SeedDeliveryAsync(Guid projectId, DeliveryStatus status)
    {
        Guid id = default;
        await WithDatabaseAsync(async db =>
        {
            var project = await db.Projects.SingleAsync(p => p.ProjectId == projectId);
            var endpoint = project.RegisterEndpoint("https://receiver.example/webhook", "encrypted-secret", "Receiver");
            db.WebhookEndpoints.Add(endpoint);
            var evt = Event.Create(projectId, "order.created", "{\"orderId\":42}");
            var delivery = Delivery.Create(evt.EventId, endpoint.EndpointId);
            if (status is DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.DeadLetter or DeliveryStatus.RetryScheduled)
            {
                delivery.RecordAttempt(status == DeliveryStatus.Delivered ? 200 : 503, "receiver unavailable",
                    status == DeliveryStatus.Delivered, DateTime.UtcNow, "unavailable", 23);
                if (status == DeliveryStatus.DeadLetter) delivery.MarkDeadLetter();
                if (status == DeliveryStatus.RetryScheduled) delivery.ScheduleRetry(DateTime.UtcNow.AddMinutes(1));
            }
            db.Events.Add(evt);
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync();
            if (status == DeliveryStatus.Processing)
                await db.Deliveries.Where(d => d.DeliveryId == delivery.DeliveryId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DeliveryStatus.Processing)
                        .SetProperty(d => d.LeaseToken, Guid.NewGuid()).SetProperty(d => d.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(1)));
            id = delivery.DeliveryId;
        });
        return id;
    }
}
