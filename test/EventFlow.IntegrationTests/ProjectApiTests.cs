using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests : IAsyncLifetime
{
    private readonly EventFlowApiFactory _factory = new();

    public Task InitializeAsync() => WithDatabaseAsync(async db => { await db.Database.EnsureCreatedAsync(); });

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task CreateProject_AuthenticatedUser_PersistsProjectAndOwnerMembership()
    {
        var owner = await RegisterAndLoginAsync();
        var before = DateTime.UtcNow;

        using var response = await owner.Client.PostAsJsonAsync("/api/projects", new { Name = "Customer events" });

        await AssertStatusAsync(HttpStatusCode.Created, response);
        var project = await ReadAsync<ProjectResponse>(response);
        Assert.NotEqual(Guid.Empty, project.ProjectId);
        Assert.Equal("Customer events", project.Name);
        Assert.Equal("Owner", project.Role);
        Assert.InRange(project.CreatedAt, before, DateTime.UtcNow);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/projects/{project.ProjectId}", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);

        // Follow the actual Location header, exercising routing and response serialization.
        using var getResponse = await owner.Client.GetAsync(response.Headers.Location);
        await AssertStatusAsync(HttpStatusCode.OK, getResponse);
        Assert.Equal(project, await ReadAsync<ProjectResponse>(getResponse));

        await WithDatabaseAsync(async db =>
        {
            var stored = Assert.Single(await db.Projects.AsNoTracking().ToListAsync());
            Assert.Equal(project.ProjectId, stored.ProjectId);
            Assert.Equal(project.Name, stored.Name);
            Assert.Equal(project.CreatedAt, stored.CreatedAt);
            Assert.True(stored.IsActive);
            var membership = Assert.Single(await db.ProjectMemberships.AsNoTracking().ToListAsync());
            Assert.Equal(owner.UserId, membership.UserId);
            Assert.Equal(project.ProjectId, membership.ProjectId);
            Assert.Equal(ProjectMembershipRole.Owner, membership.Role);
            Assert.True(membership.IsActive);
        });
    }

    [Fact]
    public async Task CreateProject_SpoofedUserId_UsesAuthenticatedUser()
    {
        var owner = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();

        using var response = await owner.Client.PostAsJsonAsync("/api/projects", new { Name = "Owned by caller", UserId = other.UserId });

        await AssertStatusAsync(HttpStatusCode.Created, response);
        await WithDatabaseAsync(async db =>
        {
            var membership = await db.ProjectMemberships.SingleAsync();
            Assert.Equal(owner.UserId, membership.UserId);
        });
    }

    [Fact]
    public async Task GetProject_Owner_ReturnsPersistedProject()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);

        using var response = await owner.Client.GetAsync($"/api/projects/{project.ProjectId}");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal(project, await ReadAsync<ProjectResponse>(response));
    }

    [Fact]
    public async Task GetProject_AnotherUser_ReturnsNotFoundWithoutDisclosingProject()
    {
        var owner = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client, "Private customer project");

        using var response = await other.Client.GetAsync($"/api/projects/{project.ProjectId}");

        await AssertErrorAsync(HttpStatusCode.NotFound, response,
            $"'Project' with identifier '{project.ProjectId}' was not found.");
        Assert.DoesNotContain(project.Name, await response.Content.ReadAsStringAsync());
        Assert.Equal(project.Name, (await LoadProjectAsync(project.ProjectId)).Name);
    }

    [Fact]
    public async Task GetProject_InvitedMember_ReturnsMembersRole()
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, ProjectMembershipRole.ReadOnly);

        using var response = await member.Client.GetAsync($"/api/projects/{project.ProjectId}");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal(project with { Role = "ReadOnly" }, await ReadAsync<ProjectResponse>(response));
    }

    [Fact]
    public async Task GetProjects_ReturnsOnlyOwnedAndSharedProjectsWithCorrectRoles()
    {
        var user = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();
        var owned = await CreateProjectAsync(user.Client, "Owned");
        var shared = await CreateProjectAsync(other.Client, "Shared");
        await CreateProjectAsync(other.Client, "Private");
        await AddMembershipAsync(shared.ProjectId, other.UserId, user.UserId, ProjectMembershipRole.Developer);

        using var response = await user.Client.GetAsync("/api/projects");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        var projects = await ReadAsync<ProjectResponse[]>(response);
        Assert.Equal(2, projects.Length);
        Assert.Contains(owned, projects);
        Assert.Contains(shared with { Role = "Developer" }, projects);
    }

    [Fact]
    public async Task GetProjects_NoMemberships_ReturnsEmptyArray()
    {
        var user = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();
        await CreateProjectAsync(other.Client);

        using var response = await user.Client.GetAsync("/api/projects");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Empty(await ReadAsync<ProjectResponse[]>(response));
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    [InlineData(ProjectMembershipRole.Developer)]
    public async Task UpdateProject_AllowedRole_PersistsNameAndReturnsCorrectRole(ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var editor = owner;
        if (role != ProjectMembershipRole.Owner)
        {
            editor = await RegisterAndLoginAsync();
            await AddMembershipAsync(project.ProjectId, owner.UserId, editor.UserId, role);
        }

        using var response = await editor.Client.PutAsJsonAsync($"/api/projects/{project.ProjectId}", new { Name = "Updated project" });

        await AssertStatusAsync(HttpStatusCode.OK, response);
        var expected = project with { Name = "Updated project", Role = role.ToString() };
        Assert.Equal(expected, await ReadAsync<ProjectResponse>(response));
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.Equal(expected.Name, stored.Name);
        Assert.Equal(project.CreatedAt, stored.CreatedAt);
        Assert.True(stored.IsActive);
        using var getResponse = await editor.Client.GetAsync($"/api/projects/{project.ProjectId}");
        await AssertStatusAsync(HttpStatusCode.OK, getResponse);
        Assert.Equal(expected, await ReadAsync<ProjectResponse>(getResponse));
    }

    [Fact]
    public async Task UpdateProject_ReadOnlyMember_ReturnsForbiddenWithoutChangingProject()
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, ProjectMembershipRole.ReadOnly);
        var original = await LoadProjectAsync(project.ProjectId);

        using var response = await member.Client.PutAsJsonAsync($"/api/projects/{project.ProjectId}", new { Name = "Unauthorized change" });

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.Equal(original.Name, stored.Name);
        Assert.Equal(original.UpdatedAt, stored.UpdatedAt);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task MutateProject_AnotherUser_ReturnsForbiddenWithoutChangingProject(string operation)
    {
        var owner = await RegisterAndLoginAsync();
        var other = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        if (operation == "activate") await DeactivateAsync(owner.Client, project.ProjectId);
        var original = await LoadProjectAsync(project.ProjectId);

        using var response = await SendMutationAsync(other.Client, project.ProjectId, operation);

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.Equal(original.Name, stored.Name);
        Assert.Equal(original.IsActive, stored.IsActive);
        Assert.Equal(original.UpdatedAt, stored.UpdatedAt);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task ProjectEndpoint_UnknownProject_ReturnsNotFound(string operation)
    {
        var user = await RegisterAndLoginAsync();
        var projectId = Guid.NewGuid();

        using var response = operation == "get"
            ? await user.Client.GetAsync($"/api/projects/{projectId}")
            : await SendMutationAsync(user.Client, projectId, operation);

        await AssertErrorAsync(HttpStatusCode.NotFound, response, $"'Project' with identifier '{projectId}' was not found.");
        await AssertNoProjectsAsync();
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("GET", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("PUT", "/{id}")]
    [InlineData("POST", "/{id}/activate")]
    [InlineData("POST", "/{id}/deactivate")]
    public async Task ProjectEndpoint_Unauthenticated_ReturnsUnauthorized(string method, string suffix)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        using var anonymous = _factory.CreateApiClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/projects" + suffix.Replace("{id}", project.ProjectId.ToString()));
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { Name = "Unauthorized" });

        using var response = await anonymous.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        await WithDatabaseAsync(async db => Assert.Single(await db.Projects.ToListAsync()));
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.Equal(project.Name, stored.Name);
        Assert.True(stored.IsActive);
    }

    [Theory]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-signature", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-issuer", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-user-id", HttpStatusCode.Forbidden)]
    [InlineData("missing-user-id", HttpStatusCode.Forbidden)]
    public async Task CreateProject_InvalidCredentials_RejectsRequestWithoutPersisting(string tokenKind, HttpStatusCode expected)
    {
        using var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateInvalidToken(tokenKind));

        using var response = await client.PostAsJsonAsync("/api/projects", new { Name = "Unauthorized" });

        await AssertStatusAsync(expected, response);
        await AssertNoProjectsAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateProject_BlankName_ReturnsValidationErrorWithoutPersisting(string name)
    {
        var user = await RegisterAndLoginAsync();

        using var response = await user.Client.PostAsJsonAsync("/api/projects", new { Name = name });

        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, "Project name is required");
        await AssertNoProjectsAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateProject_BlankName_ReturnsValidationErrorWithoutChangingProject(string name)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var original = await LoadProjectAsync(project.ProjectId);

        using var response = await owner.Client.PutAsJsonAsync($"/api/projects/{project.ProjectId}", new { Name = name });

        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, "Project name is required");
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.Equal(original.Name, stored.Name);
        Assert.Equal(original.UpdatedAt, stored.UpdatedAt);
    }

    [Theory]
    [InlineData("POST", "{}")]
    [InlineData("POST", "{\"name\":null}")]
    [InlineData("POST", "null")]
    [InlineData("POST", "{")]
    [InlineData("PUT", "{}")]
    [InlineData("PUT", "{\"name\":null}")]
    [InlineData("PUT", "null")]
    [InlineData("PUT", "{")]
    public async Task ProjectWrite_InvalidBody_ReturnsBadRequestWithoutChangingData(string method, string body)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        using var request = new HttpRequestMessage(new HttpMethod(method), method == "POST" ? "/api/projects" : $"/api/projects/{project.ProjectId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await owner.Client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await WithDatabaseAsync(async db =>
        {
            var stored = Assert.Single(await db.Projects.AsNoTracking().ToListAsync());
            Assert.Equal(project.Name, stored.Name);
            Assert.Single(await db.ProjectMemberships.ToListAsync());
        });
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "")]
    [InlineData("POST", "/activate")]
    [InlineData("POST", "/deactivate")]
    public async Task ProjectEndpoint_MalformedProjectId_ReturnsBadRequest(string method, string suffix)
    {
        var user = await RegisterAndLoginAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/projects/not-a-guid{suffix}");
        if (method == "PUT") request.Content = JsonContent.Create(new { Name = "Renamed" });

        using var response = await user.Client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        await AssertNoProjectsAsync();
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task DeactivateProject_AllowedRole_PersistsInactiveStateAndPreservesMembership(ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var actor = owner;
        if (role == ProjectMembershipRole.Admin)
        {
            actor = await RegisterAndLoginAsync();
            await AddMembershipAsync(project.ProjectId, owner.UserId, actor.UserId, role);
        }

        using var response = await actor.Client.PostAsync($"/api/projects/{project.ProjectId}/deactivate", null);

        await AssertStatusAsync(HttpStatusCode.NoContent, response);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var stored = await LoadProjectAsync(project.ProjectId);
        Assert.False(stored.IsActive);
        Assert.Equal(project.Name, stored.Name);
        await WithDatabaseAsync(async db =>
        {
            var membership = await db.ProjectMemberships.SingleAsync(m => m.ProjectId == project.ProjectId && m.UserId == actor.UserId);
            Assert.Equal(role, membership.Role);
            Assert.True(membership.IsActive);
        });
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task ActivateProject_AllowedRole_ReactivatesPersistedProject(ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await DeactivateAsync(owner.Client, project.ProjectId);
        var actor = owner;
        if (role == ProjectMembershipRole.Admin)
        {
            actor = await RegisterAndLoginAsync();
            await AddMembershipAsync(project.ProjectId, owner.UserId, actor.UserId, role);
        }

        using var response = await actor.Client.PostAsync($"/api/projects/{project.ProjectId}/activate", null);

        await AssertStatusAsync(HttpStatusCode.NoContent, response);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.True((await LoadProjectAsync(project.ProjectId)).IsActive);
    }

    [Theory]
    [InlineData("activate", ProjectMembershipRole.Developer)]
    [InlineData("activate", ProjectMembershipRole.ReadOnly)]
    [InlineData("deactivate", ProjectMembershipRole.Developer)]
    [InlineData("deactivate", ProjectMembershipRole.ReadOnly)]
    public async Task ToggleProjectStatus_InsufficientRole_ReturnsForbiddenWithoutChangingState(string operation, ProjectMembershipRole role)
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, role);
        if (operation == "activate") await DeactivateAsync(owner.Client, project.ProjectId);
        var original = await LoadProjectAsync(project.ProjectId);

        using var response = await SendMutationAsync(member.Client, project.ProjectId, operation);

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
        Assert.Equal(original.IsActive, (await LoadProjectAsync(project.ProjectId)).IsActive);
    }

    [Theory]
    [InlineData("activate", true, "Project is already active")]
    [InlineData("deactivate", false, "Project is already inactive")]
    public async Task ToggleProjectStatus_AlreadyInRequestedState_ReturnsValidationError(string operation, bool expectedActive, string error)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        if (!expectedActive) await DeactivateAsync(owner.Client, project.ProjectId);

        using var response = await SendMutationAsync(owner.Client, project.ProjectId, operation);

        await AssertErrorAsync(HttpStatusCode.UnprocessableEntity, response, error);
        Assert.Equal(expectedActive, (await LoadProjectAsync(project.ProjectId)).IsActive);
    }

    private async Task<AuthenticatedUser> RegisterAndLoginAsync()
    {
        var client = _factory.CreateApiClient();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "IntegrationPass123!";
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterInput(email, password, "Test", "User"));
        await AssertStatusAsync(HttpStatusCode.OK, registration);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginInput(email, password));
        await AssertStatusAsync(HttpStatusCode.OK, login);
        var token = (await ReadAsync<LoginResponse>(login)).Token;
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventFlowDbContext>();
        var user = await db.DomainUsers.AsNoTracking().SingleAsync(u => u.Email == email);
        return new AuthenticatedUser(client, user.UserId);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string name = "Original project")
    {
        using var response = await client.PostAsJsonAsync("/api/projects", new { Name = name });
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return await ReadAsync<ProjectResponse>(response);
    }

    private static async Task DeactivateAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.PostAsync($"/api/projects/{projectId}/deactivate", null);
        await AssertStatusAsync(HttpStatusCode.NoContent, response);
    }

    private static Task<HttpResponseMessage> SendMutationAsync(HttpClient client, Guid projectId, string operation) => operation switch
    {
        "update" => client.PutAsJsonAsync($"/api/projects/{projectId}", new { Name = "Renamed" }),
        "activate" or "deactivate" => client.PostAsync($"/api/projects/{projectId}/{operation}", null),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    // Invitations have no HTTP endpoint yet; only this prerequisite is seeded directly.
    private Task AddMembershipAsync(Guid projectId, Guid ownerId, Guid memberId, ProjectMembershipRole role) =>
        WithDatabaseAsync(async db =>
        {
            var owner = await db.ProjectMemberships.SingleAsync(m => m.ProjectId == projectId && m.UserId == ownerId);
            db.ProjectMemberships.Add(owner.Invite(memberId, role));
            await db.SaveChangesAsync();
        });

    private async Task<Project> LoadProjectAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventFlowDbContext>();
        return await db.Projects.AsNoTracking().SingleAsync(p => p.ProjectId == projectId);
    }

    private async Task WithDatabaseAsync(Func<EventFlowDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<EventFlowDbContext>());
    }

    private Task AssertNoProjectsAsync() => WithDatabaseAsync(async db =>
    {
        Assert.Empty(await db.Projects.ToListAsync());
        Assert.Empty(await db.ProjectMemberships.ToListAsync());
    });

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) where T : class =>
        Assert.IsType<T>(await response.Content.ReadFromJsonAsync<T>());

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected} {expected}, received {(int)response.StatusCode} {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task AssertErrorAsync(HttpStatusCode expected, HttpResponseMessage response, string message)
    {
        await AssertStatusAsync(expected, response);
        Assert.Equal(message, (await ReadAsync<ErrorResponse>(response)).Error);
    }

    private static string CreateInvalidToken(string kind)
    {
        if (kind == "malformed") return "not-a-jwt";
        var claims = new List<Claim>();
        if (kind != "missing-user-id")
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, kind == "invalid-user-id" ? "not-a-guid" : Guid.NewGuid().ToString()));

        var key = kind == "wrong-signature" ? "a-different-integration-test-signing-key-32-bytes" : EventFlowApiFactory.JwtSecret;
        var token = new JwtSecurityToken(
            issuer: kind == "wrong-issuer" ? "UntrustedIssuer" : EventFlowApiFactory.JwtIssuer,
            audience: kind == "wrong-audience" ? "OtherAudience" : EventFlowApiFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: kind == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record AuthenticatedUser(HttpClient Client, Guid UserId);
    private sealed record LoginResponse(string Token);
    private sealed record ErrorResponse(string Error);
}
