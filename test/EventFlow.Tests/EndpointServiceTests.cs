using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Application.Services;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;

namespace EventFlow.Tests;

public sealed class EndpointServiceTests
{
    private readonly ProjectServiceTests.ProjectRepositoryStub _projects = new();
    private readonly EndpointRepositoryStub _endpoints = new();
    private readonly SigningKeyStub _keys = new();
    private readonly ProjectCreationResponse _project = Project.Create("Webhooks", Guid.NewGuid());
    private readonly EndpointService _service;
    private Guid ProjectId => _project.Project.ProjectId;
    private Guid UserId => _project.Membership.UserId;

    public EndpointServiceTests()
    {
        _projects.Projects.Add(_project.Project);
        _projects.Memberships.Add(_project.Membership);
        _service = new EndpointService(_endpoints, _keys, _projects);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task Create_AllowedRole_PersistsEncryptedSecretAndReturnsPlaintext(ProjectMembershipRole role)
    {
        var userId = Member(role);
        var before = DateTime.UtcNow;
        var result = await _service.CreateEndpointAsync(new(ProjectId, userId, "https://example.test/hook", "Orders"));

        var stored = Assert.Single(_endpoints.Items);
        Assert.Equal("encrypted-secret", stored.SigningSecret.Value);
        Assert.Equal("plain-secret", Assert.Single(_keys.Encrypted));
        Assert.Equal(new EndpointCreationOutput(stored.EndpointId, ProjectId, stored.Url, "plain-secret", "Orders", true, stored.CreatedAt), result);
        Assert.NotEqual(Guid.Empty, result.EndpointId);
        Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal((userId, ProjectId), Assert.Single(_projects.MembershipLookups));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("http://example.test/hook")]
    [InlineData("/relative")]
    [InlineData("not-a-url")]
    public async Task Create_InvalidUrl_DoesNotPersist(string? url)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateEndpointAsync(new(ProjectId, UserId, url!, "Orders")));
        Assert.Empty(_endpoints.Items);
    }

    [Fact]
    public async Task Create_InactiveProject_DoesNotPersist()
    {
        _project.Project.Deactivate();
        await Assert.ThrowsAsync<ValidationException>(() => Invoke("create", Guid.NewGuid(), UserId));
        Assert.Empty(_endpoints.Items);
    }

    public static IEnumerable<object[]> Operations =>
        new[] { "create", "get", "list", "update", "delete", "activate", "deactivate" }.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task NonMember_RejectsBeforeAccessingEndpoints(string operation)
    {
        var expected = operation is "delete" or "activate" or "deactivate" ? typeof(NotFoundException) : typeof(ForbiddenException);
        await Assert.ThrowsAsync(expected, () => Invoke(operation, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(0, _endpoints.Reads);
        Assert.Equal(0, _endpoints.Writes);
        Assert.Empty(_keys.Encrypted);
    }

    public static IEnumerable<object[]> DeniedMutations =>
        from operation in new[] { "create", "update", "delete", "activate", "deactivate" }
        from role in new[] { ProjectMembershipRole.Developer, ProjectMembershipRole.ReadOnly }
        select new object[] { operation, role };

    [Theory]
    [MemberData(nameof(DeniedMutations))]
    public async Task Mutation_InsufficientRole_DoesNotReadOrWriteEndpoints(string operation, ProjectMembershipRole role)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Invoke(operation, Guid.NewGuid(), Member(role)));
        Assert.Equal(0, _endpoints.Reads);
        Assert.Equal(0, _endpoints.Writes);
        Assert.Empty(_keys.Encrypted);
    }

    public static IEnumerable<object[]> MissingEndpoints =>
        from operation in new[] { "get", "update", "delete", "activate", "deactivate" }
        from otherProject in new[] { false, true }
        select new object[] { operation, otherProject };

    [Theory]
    [MemberData(nameof(MissingEndpoints))]
    public async Task Endpoint_MissingOrFromAnotherProject_ThrowsWithoutWriting(string operation, bool otherProject)
    {
        var endpoint = Seed(otherProject ? Project.Create("Other", Guid.NewGuid()).Project : _project.Project);
        if (!otherProject) _endpoints.Items.Clear();
        await Assert.ThrowsAsync<NotFoundException>(() => Invoke(operation, endpoint.EndpointId, UserId));
        Assert.Equal(0, _endpoints.Writes);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task Read_AllRoles_MapsEndpointsAndFiltersListByProject(ProjectMembershipRole role)
    {
        var endpoint = Seed(_project.Project);
        Seed(Project.Create("Other", Guid.NewGuid()).Project);
        var userId = Member(role);
        var expected = Output(endpoint);
        Assert.Equal(expected, await _service.GetEndpointAsync(endpoint.EndpointId, ProjectId, userId));
        Assert.Equal(expected, Assert.Single(await _service.GetEndpointsAsync(ProjectId, userId)));
    }

    [Fact]
    public async Task List_NoEndpoints_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetEndpointsAsync(ProjectId, UserId));
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task Mutations_AllowedRole_UpdateToggleAndDelete(ProjectMembershipRole role)
    {
        var endpoint = Seed(_project.Project);
        var userId = Member(role);
        var createdAt = endpoint.CreatedAt;
        var result = await _service.UpdateEndpointAsync(new(endpoint.EndpointId, ProjectId, userId, "https://updated.test/hook", "Updated"));
        Assert.Equal(Output(endpoint), result);
        Assert.Equal("https://updated.test/hook", result.Url);
        Assert.Equal("Updated", result.Description);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal("encrypted-secret", endpoint.SigningSecret.Value);
        Assert.Equal(1, _endpoints.Saves);
        await _service.DeactivateEndpointAsync(endpoint.EndpointId, ProjectId, userId);
        Assert.False(endpoint.IsActive);
        await _service.ActivateEndpointAsync(endpoint.EndpointId, ProjectId, userId);
        Assert.True(endpoint.IsActive);
        await _service.DeleteEndpointAsync(endpoint.EndpointId, ProjectId, userId);
        Assert.Empty(_endpoints.Items);
        Assert.Equal(4, _endpoints.Writes);
    }

    [Fact]
    public async Task Update_InvalidUrl_DoesNotMutateOrPersist()
    {
        var endpoint = Seed(_project.Project);
        var original = Output(endpoint);
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateEndpointAsync(new(endpoint.EndpointId, ProjectId, UserId, "http://unsafe.test", "Changed")));
        Assert.Equal(original, Output(endpoint));
        Assert.Equal(0, _endpoints.Writes);
    }

    [Fact]
    public async Task Update_BlankFields_PreservesExistingValues()
    {
        var endpoint = Seed(_project.Project);
        var original = Output(endpoint);
        Assert.Equal(original, await _service.UpdateEndpointAsync(new(endpoint.EndpointId, ProjectId, UserId, " ", "")));
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Toggle_AlreadyInRequestedState_DoesNotPersist(string operation)
    {
        var endpoint = Seed(_project.Project);
        if (operation == "deactivate") endpoint.Deactivate();
        await Assert.ThrowsAsync<ValidationException>(() => Invoke(operation, endpoint.EndpointId, UserId));
        Assert.Equal(operation == "activate", endpoint.IsActive);
        Assert.Equal(0, _endpoints.Writes);
    }

    private Guid Member(ProjectMembershipRole role)
    {
        if (role == ProjectMembershipRole.Owner) return UserId;
        var membership = _project.Membership.Invite(Guid.NewGuid(), role);
        _projects.Memberships.Add(membership);
        return membership.UserId;
    }

    private WebhookEndpoint Seed(Project project)
    {
        var endpoint = project.RegisterEndpoint("https://example.test/hook", "encrypted-secret", "Orders");
        _endpoints.Items.Add(endpoint);
        return endpoint;
    }

    private static EndpointOutput Output(WebhookEndpoint endpoint) =>
        new(endpoint.EndpointId, endpoint.ProjectId, endpoint.Url, endpoint.Description, endpoint.IsActive, endpoint.CreatedAt);

    private Task Invoke(string operation, Guid endpointId, Guid userId) => operation switch
    {
        "create" => _service.CreateEndpointAsync(new(ProjectId, userId, "https://example.test/hook", "Orders")),
        "get" => _service.GetEndpointAsync(endpointId, ProjectId, userId),
        "list" => _service.GetEndpointsAsync(ProjectId, userId),
        "update" => _service.UpdateEndpointAsync(new(endpointId, ProjectId, userId, "https://updated.test/hook", "Updated")),
        "delete" => _service.DeleteEndpointAsync(endpointId, ProjectId, userId),
        "activate" => _service.ActivateEndpointAsync(endpointId, ProjectId, userId),
        "deactivate" => _service.DeactivateEndpointAsync(endpointId, ProjectId, userId),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private sealed class SigningKeyStub : ISigningKeyService
    {
        public List<string> Encrypted { get; } = [];
        public string GenerateSigningKey() => "plain-secret";
        public string Encrypt(string key) { Encrypted.Add(key); return "encrypted-secret"; }
        public string Decrypt(string key) => throw new NotSupportedException();
    }

    private sealed class EndpointRepositoryStub : IEndpointRepository
    {
        public List<WebhookEndpoint> Items { get; } = [];
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int Saves { get; private set; }
        public Task<WebhookEndpoint> CreateEndpointAsync(WebhookEndpoint endpoint)
        { Writes++; Items.Add(endpoint); return Task.FromResult(endpoint); }
        public Task<WebhookEndpoint?> GetEndpointByIdAsync(Guid id)
        { Reads++; return Task.FromResult(Items.SingleOrDefault(e => e.EndpointId == id)); }
        public Task<IEnumerable<WebhookEndpoint>> GetAllEndpointsAsync(Guid projectId)
        { Reads++; return Task.FromResult<IEnumerable<WebhookEndpoint>>(Items.Where(e => e.ProjectId == projectId).ToArray()); }
        public Task<WebhookEndpoint> UpdateEndpointAsync(WebhookEndpoint endpoint)
        { Writes++; return Task.FromResult(endpoint); }
        public Task DeleteEndpointAsync(Guid id)
        { Writes++; Items.RemoveAll(e => e.EndpointId == id); return Task.CompletedTask; }
        public Task SaveChangesAsync() { Saves++; return Task.CompletedTask; }
    }
}
