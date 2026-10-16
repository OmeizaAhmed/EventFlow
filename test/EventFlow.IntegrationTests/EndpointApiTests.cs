using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.IntegrationTests;

// Shares the existing per-test factory, real login, and project setup helpers.
public sealed partial class ProjectApiTests
{
    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task EndpointLifecycle_AllowedRole_PersistsChangesAndOnlyReturnsSecretOnCreation(ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var actor = owner;
        if (role == ProjectMembershipRole.Admin)
        {
            actor = await RegisterAndLoginAsync();
            await AddMembershipAsync(project.ProjectId, owner.UserId, actor.UserId, role);
        }

        var path = EndpointPath(project.ProjectId);
        using var creation = await actor.Client.PostAsJsonAsync(path, new EndpointRequest("https://example.test/hook", "Orders"));
        await AssertStatusAsync(HttpStatusCode.Created, creation);
        var endpoint = await ReadAsync<EndpointCreationOutput>(creation);
        Assert.Equal(project.ProjectId, endpoint.ProjectId);
        Assert.NotEqual(Guid.Empty, endpoint.EndpointId);
        Assert.True(endpoint.IsActive);
        Assert.StartsWith("whsec_", endpoint.SigningSecret);
        Assert.Equal(32, Convert.FromBase64String(endpoint.SigningSecret[6..]).Length);
        Assert.NotNull(creation.Headers.Location);
        Assert.EndsWith($"{path}/{endpoint.EndpointId}", creation.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
        var stored = await LoadEndpointAsync(endpoint.EndpointId);
        Assert.NotEqual(endpoint.SigningSecret, stored.SigningSecret.Value);
        using (var scope = _factory.Services.CreateScope())
            Assert.Equal(endpoint.SigningSecret, scope.ServiceProvider.GetRequiredService<ISigningKeyService>().Decrypt(stored.SigningSecret.Value));

        using var get = await actor.Client.GetAsync(creation.Headers.Location);
        await AssertStatusAsync(HttpStatusCode.OK, get);
        Assert.Equal(new EndpointOutput(endpoint.EndpointId, project.ProjectId, endpoint.Url, endpoint.Description, true, endpoint.CreatedAt), await ReadAsync<EndpointOutput>(get));
        await AssertNoEndpointSecretAsync(get);

        using var update = await actor.Client.PutAsJsonAsync($"{path}/{endpoint.EndpointId}", new EndpointRequest("https://updated.test/hook", "Updated"));
        await AssertStatusAsync(HttpStatusCode.OK, update);
        var updated = await ReadAsync<EndpointOutput>(update);
        Assert.Equal("https://updated.test/hook", updated.Url);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(endpoint.CreatedAt, updated.CreatedAt);
        await AssertNoEndpointSecretAsync(update);
        var persisted = await LoadEndpointAsync(endpoint.EndpointId);
        Assert.Equal(updated.Url, persisted.Url);
        Assert.Equal(updated.Description, persisted.Description);
        Assert.Equal(stored.SigningSecret.Value, persisted.SigningSecret.Value);

        foreach (var operation in new[] { "deactivate", "activate" })
        {
            using var toggle = await SendEndpointRequestAsync(actor.Client, project.ProjectId, endpoint.EndpointId, operation);
            await AssertStatusAsync(HttpStatusCode.NoContent, toggle);
            Assert.Empty(await toggle.Content.ReadAsStringAsync());
            Assert.Equal(operation == "activate", (await LoadEndpointAsync(endpoint.EndpointId)).IsActive);
        }

        using var delete = await actor.Client.DeleteAsync($"{path}/{endpoint.EndpointId}");
        await AssertStatusAsync(HttpStatusCode.NoContent, delete);
        await WithDatabaseAsync(async db => Assert.False(await db.WebhookEndpoints.AnyAsync(e => e.EndpointId == endpoint.EndpointId)));
        using var missing = await actor.Client.GetAsync($"{path}/{endpoint.EndpointId}");
        await AssertStatusAsync(HttpStatusCode.NotFound, missing);
        using var list = await actor.Client.GetAsync(path);
        await AssertStatusAsync(HttpStatusCode.OK, list);
        Assert.Empty(await ReadAsync<EndpointOutput[]>(list));
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task EndpointRead_Member_CanReadAndListOnlyRequestedProject(ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var other = await CreateProjectAsync(owner.Client, "Other");
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        await CreateEndpointAsync(owner.Client, other.ProjectId);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, role);

        using var get = await SendEndpointRequestAsync(member.Client, project.ProjectId, endpoint.EndpointId, "get");
        await AssertStatusAsync(HttpStatusCode.OK, get);
        Assert.Equal(endpoint.EndpointId, (await ReadAsync<EndpointOutput>(get)).EndpointId);
        await AssertNoEndpointSecretAsync(get);
        using var list = await member.Client.GetAsync(EndpointPath(project.ProjectId));
        await AssertStatusAsync(HttpStatusCode.OK, list);
        Assert.Equal(endpoint.EndpointId, Assert.Single(await ReadAsync<EndpointOutput[]>(list)).EndpointId);
        await AssertNoEndpointSecretAsync(list);
    }

    public static IEnumerable<object[]> EndpointOperations =>
        new[] { "create", "get", "list", "update", "delete", "activate", "deactivate" }.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(EndpointOperations))]
    public async Task EndpointRoute_Anonymous_ReturnsUnauthorized(string operation)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        using var anonymous = _factory.CreateApiClient();
        using var response = await SendEndpointRequestAsync(anonymous, project.ProjectId, endpoint.EndpointId, operation);
        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        await AssertEndpointUnchangedAsync(endpoint);
    }

    [Theory]
    [MemberData(nameof(EndpointOperations))]
    public async Task EndpointRoute_NonMember_RejectsWithoutChangingData(string operation)
    {
        var owner = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        using var response = await SendEndpointRequestAsync(other.Client, project.ProjectId, endpoint.EndpointId, operation);
        await AssertStatusAsync(operation is "delete" or "activate" or "deactivate" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response);
        await AssertEndpointUnchangedAsync(endpoint);
    }

    public static IEnumerable<object[]> EndpointDeniedMutations =>
        from operation in new[] { "create", "update", "delete", "activate", "deactivate" }
        from role in new[] { ProjectMembershipRole.Developer, ProjectMembershipRole.ReadOnly }
        select new object[] { operation, role };

    [Theory]
    [MemberData(nameof(EndpointDeniedMutations))]
    public async Task EndpointMutation_InsufficientRole_ReturnsForbidden(string operation, ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, role);
        using var response = await SendEndpointRequestAsync(member.Client, project.ProjectId, endpoint.EndpointId, operation);
        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
        await AssertEndpointUnchangedAsync(endpoint);
    }

    public static IEnumerable<object[]> EndpointMissingCases =>
        from operation in new[] { "get", "update", "delete", "activate", "deactivate" }
        from otherProject in new[] { false, true }
        select new object[] { operation, otherProject };

    [Theory]
    [MemberData(nameof(EndpointMissingCases))]
    public async Task EndpointRoute_UnknownOrWrongProject_ReturnsNotFound(string operation, bool otherProject)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        var targetProject = otherProject ? (await CreateProjectAsync(owner.Client, "Other")).ProjectId : project.ProjectId;
        using var response = await SendEndpointRequestAsync(owner.Client, targetProject, otherProject ? endpoint.EndpointId : Guid.NewGuid(), operation);
        await AssertStatusAsync(HttpStatusCode.NotFound, response);
        await AssertEndpointUnchangedAsync(endpoint);
    }

    [Theory]
    [InlineData("create", "http://example.test/hook")]
    [InlineData("create", "relative/path")]
    [InlineData("create", " ")]
    [InlineData("update", "http://example.test/hook")]
    [InlineData("update", "relative/path")]
    public async Task EndpointWrite_InvalidUrl_ReturnsValidationErrorWithoutChangingData(string operation, string url)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        var body = new EndpointRequest(url, "Changed");
        using var response = operation == "create"
            ? await owner.Client.PostAsJsonAsync(EndpointPath(project.ProjectId), body)
            : await owner.Client.PutAsJsonAsync($"{EndpointPath(project.ProjectId)}/{endpoint.EndpointId}", body);
        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, "Webhook endpoint URL must be a valid HTTPS URL");
        await AssertEndpointUnchangedAsync(endpoint);
    }

    [Theory]
    [InlineData("POST", "{}")]
    [InlineData("PUT", "{}")]
    [InlineData("POST", "{")]
    [InlineData("PUT", "null")]
    public async Task EndpointWrite_InvalidBody_ReturnsBadRequest(string method, string body)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        using var request = new HttpRequestMessage(new HttpMethod(method), EndpointPath(project.ProjectId) + (method == "PUT" ? $"/{endpoint.EndpointId}" : ""))
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        using var response = await owner.Client.SendAsync(request);
        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        await AssertEndpointUnchangedAsync(endpoint);
    }

    [Fact]
    public async Task CreateEndpoint_InactiveProject_ReturnsValidationError()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await DeactivateAsync(owner.Client, project.ProjectId);
        using var response = await SendEndpointRequestAsync(owner.Client, project.ProjectId, Guid.NewGuid(), "create");
        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, "Cannot register an endpoint on an inactive project");
        await WithDatabaseAsync(async db => Assert.Empty(await db.WebhookEndpoints.ToListAsync()));
    }

    [Theory]
    [InlineData("activate", "Endpoint is already active")]
    [InlineData("deactivate", "Endpoint is already inactive")]
    public async Task EndpointToggle_AlreadyInRequestedState_ReturnsValidationError(string operation, string error)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var endpoint = await CreateEndpointAsync(owner.Client, project.ProjectId);
        if (operation == "deactivate")
        {
            using var first = await SendEndpointRequestAsync(owner.Client, project.ProjectId, endpoint.EndpointId, operation);
            await AssertStatusAsync(HttpStatusCode.NoContent, first);
        }
        using var response = await SendEndpointRequestAsync(owner.Client, project.ProjectId, endpoint.EndpointId, operation);
        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, error);
        Assert.Equal(operation == "activate", (await LoadEndpointAsync(endpoint.EndpointId)).IsActive);
    }

    private static string EndpointPath(Guid projectId) => $"/api/projects/{projectId}/endpoints";

    private static Task<HttpResponseMessage> SendEndpointRequestAsync(HttpClient client, Guid projectId, Guid endpointId, string operation)
    {
        var path = EndpointPath(projectId);
        return operation switch
        {
            "create" => client.PostAsJsonAsync(path, new EndpointRequest("https://example.test/hook", "Orders")),
            "get" => client.GetAsync($"{path}/{endpointId}"),
            "list" => client.GetAsync(path),
            "update" => client.PutAsJsonAsync($"{path}/{endpointId}", new EndpointRequest("https://updated.test/hook", "Changed")),
            "delete" => client.DeleteAsync($"{path}/{endpointId}"),
            "activate" or "deactivate" => client.PostAsync($"{path}/{endpointId}/{operation}", null),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static async Task<EndpointCreationOutput> CreateEndpointAsync(HttpClient client, Guid projectId)
    {
        using var response = await SendEndpointRequestAsync(client, projectId, Guid.NewGuid(), "create");
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return await ReadAsync<EndpointCreationOutput>(response);
    }

    private async Task<WebhookEndpoint> LoadEndpointAsync(Guid endpointId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EventFlowDbContext>().WebhookEndpoints.AsNoTracking().SingleAsync(e => e.EndpointId == endpointId);
    }

    private async Task AssertEndpointUnchangedAsync(EndpointCreationOutput original)
    {
        var stored = await LoadEndpointAsync(original.EndpointId);
        Assert.Equal(original.Url, stored.Url);
        Assert.Equal(original.Description, stored.Description);
        Assert.Equal(original.IsActive, stored.IsActive);
        Assert.Equal(original.CreatedAt, stored.CreatedAt);
        await WithDatabaseAsync(async db => Assert.Single(await db.WebhookEndpoints.ToListAsync()));
    }

    private static async Task AssertNoEndpointSecretAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var elements = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement.EnumerateArray().ToArray() : [json.RootElement];
        foreach (var element in elements)
            Assert.DoesNotContain(element.EnumerateObject(), property => property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }
}
