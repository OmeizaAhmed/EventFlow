using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Application.Services;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;
using EventFlow.Infrastructure.Services;

namespace EventFlow.Tests;


public partial class ProjectServiceTests
{
    private readonly ProjectRepositoryStub _repository = new();
    private readonly ProjectService _service;
    
    private readonly ProjectCreationResponse _creation = Project.Create("Original project", Guid.NewGuid());

    public ProjectServiceTests()
    {
        _service = new ProjectService(_repository, new ApiKeyService());
        _repository.Projects.Add(_creation.Project);
        _repository.Memberships.Add(_creation.Membership);
    }

    private Guid ProjectId => _creation.Project.ProjectId;
    private Guid OwnerId => _creation.Membership.UserId;

    [Fact]
    public async Task CreateProjectAsync_ValidInput_PersistsProjectAndOwnerAndMapsResponse()
    {
        var userId = Guid.NewGuid();

        var result = await _service.CreateProjectAsync(userId, "New project");

        var added = Assert.Single(_repository.AddedProjects);
        Assert.NotEqual(Guid.Empty, added.Project.ProjectId);
        Assert.Equal("New project", added.Project.Name);
        Assert.True(added.Project.IsActive);
        Assert.Equal(userId, added.Membership.UserId);
        Assert.Equal(added.Project.ProjectId, added.Membership.ProjectId);
        Assert.Equal(ProjectMembershipRole.Owner, added.Membership.Role);
        Assert.Equal(new ProjectResponse(added.Project.ProjectId, "New project", added.Project.CreatedAt, "Owner"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateProjectAsync_InvalidName_DoesNotPersist(string? name)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateProjectAsync(OwnerId, name!));
        Assert.Empty(_repository.AddedProjects);
    }

    [Fact]
    public async Task CreateProjectAsync_EmptyUserId_DoesNotPersist()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateProjectAsync(Guid.Empty, "Project"));
        Assert.Empty(_repository.AddedProjects);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task GetProjectByIdAsync_Member_ReturnsProjectWithMembersRole(ProjectMembershipRole role)
    {
        var member = AddMember(role);

        var result = await _service.GetProjectByIdAsync(ProjectId, member.UserId);

        Assert.Equal(new ProjectResponse(ProjectId, _creation.Project.Name, _creation.Project.CreatedAt, role.ToString()), result);
    }

    [Fact]
    public async Task GetProjectByIdAsync_NonMember_HidesProjectWithoutLoadingIt()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetProjectByIdAsync(ProjectId, Guid.NewGuid()));

        Assert.Contains(ProjectId.ToString(), exception.Message);
        Assert.Empty(_repository.ProjectLookups);
    }

    [Fact]
    public async Task GetProjectByIdAsync_MissingProject_ThrowsNotFound()
    {
        _repository.Projects.Clear();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetProjectByIdAsync(ProjectId, OwnerId));
    }

    [Fact]
    public async Task GetProjectsByUserIdAsync_MapsAllProjectsAndPassesUserId()
    {
        _repository.UserProjects =
        [
            new(ProjectId, "First", new DateTime(2026, 1, 1), "Owner"),
            new(Guid.NewGuid(), "Second", new DateTime(2026, 2, 1), "ReadOnly")
        ];

        var results = (await _service.GetProjectsByUserIdAsync(OwnerId)).ToArray();

        Assert.Equal(OwnerId, Assert.Single(_repository.UserLookups));
        Assert.Equal(_repository.UserProjects.Select(p => new ProjectResponse(p.ProjectId, p.Name, p.CreatedAt, p.Role)), results);
    }

    [Fact]
    public async Task GetProjectsByUserIdAsync_NoProjects_ReturnsEmptyCollection()
    {
        Assert.Empty(await _service.GetProjectsByUserIdAsync(OwnerId));
    }

    [Fact]
    public async Task UpdateProjectAsync_Member_PersistsNameAndReturnsMembershipRole()
    {
        var member = AddMember(ProjectMembershipRole.Admin);
        var createdAt = _creation.Project.CreatedAt;

        var result = await _service.UpdateProjectAsync(ProjectId, member.UserId, "Renamed");

        Assert.Same(_creation.Project, Assert.Single(_repository.UpdatedProjects));
        Assert.Equal("Renamed", _creation.Project.Name);
        Assert.Equal(new ProjectResponse(ProjectId, "Renamed", createdAt, "Admin"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateProjectAsync_InvalidName_DoesNotMutateOrPersist(string? name)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateProjectAsync(ProjectId, OwnerId, name!));
        Assert.Equal("Original project", _creation.Project.Name);
        Assert.Empty(_repository.UpdatedProjects);
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Activate")]
    [InlineData("Deactivate")]
    public async Task ProjectMutation_MissingProject_ThrowsWithoutPersisting(string operation)
    {
        _repository.Projects.Clear();

        await Assert.ThrowsAsync<NotFoundException>(() => MutateProject(operation, OwnerId));

        AssertNoProjectWrites();
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Activate")]
    [InlineData("Deactivate")]
    public async Task ProjectMutation_NonMember_ThrowsWithoutMutatingOrPersisting(string operation)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => MutateProject(operation, Guid.NewGuid()));

        Assert.Equal("Original project", _creation.Project.Name);
        Assert.True(_creation.Project.IsActive);
        AssertNoProjectWrites();
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task DeleteProjectAsync_AuthorizedRole_DeletesProject(ProjectMembershipRole role)
    {
        var member = AddMember(role);

        Assert.True(await _service.DeleteProjectAsync(ProjectId, member.UserId));
        Assert.Equal(ProjectId, Assert.Single(_repository.DeletedProjects));
        Assert.Empty(_repository.UpdatedProjects);
    }

    [Theory]
    [InlineData("Delete", ProjectMembershipRole.Developer)]
    [InlineData("Delete", ProjectMembershipRole.ReadOnly)]
    [InlineData("Activate", ProjectMembershipRole.Developer)]
    [InlineData("Activate", ProjectMembershipRole.ReadOnly)]
    [InlineData("Deactivate", ProjectMembershipRole.Developer)]
    [InlineData("Deactivate", ProjectMembershipRole.ReadOnly)]
    public async Task ProjectMutation_InsufficientRole_DoesNotPersist(string operation, ProjectMembershipRole role)
    {
        var member = AddMember(role);
        if (operation == "Activate") _creation.Project.Deactivate();
        var wasActive = _creation.Project.IsActive;

        await Assert.ThrowsAsync<ForbiddenException>(() => MutateProject(operation, member.UserId));

        Assert.Equal(wasActive, _creation.Project.IsActive);
        AssertNoProjectWrites();
    }

    [Theory]
    [InlineData(true, ProjectMembershipRole.Owner)]
    [InlineData(true, ProjectMembershipRole.Admin)]
    [InlineData(false, ProjectMembershipRole.Owner)]
    [InlineData(false, ProjectMembershipRole.Admin)]
    public async Task ToggleProjectStatus_AuthorizedRole_PersistsNewState(bool activate, ProjectMembershipRole role)
    {
        var member = AddMember(role);
        if (activate) _creation.Project.Deactivate();

        var result = activate
            ? await _service.ActivateProjectAsync(ProjectId, member.UserId)
            : await _service.DeactivateProjectAsync(ProjectId, member.UserId);

        Assert.True(result);
        Assert.Equal(activate, _creation.Project.IsActive);
        Assert.Same(_creation.Project, Assert.Single(_repository.UpdatedProjects));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ToggleProjectStatus_AlreadyInRequestedState_DoesNotPersist(bool activate)
    {
        if (!activate) _creation.Project.Deactivate();

        await Assert.ThrowsAsync<ValidationException>(() => MutateProject(activate ? "Activate" : "Deactivate", OwnerId));

        Assert.Equal(activate, _creation.Project.IsActive);
        AssertNoProjectWrites();
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Owner)]
    [InlineData(ProjectMembershipRole.Admin)]
    public async Task InviteMemberAsync_AuthorizedInviter_PersistsReadOnlyMemberAndMapsResponse(ProjectMembershipRole role)
    {
        var inviter = AddMember(role);
        var inviteeId = Guid.NewGuid();

        var result = await _service.InviteMemberAsync(inviter.UserId, inviteeId, ProjectId);

        var added = Assert.Single(_repository.AddedMemberships);
        Assert.Equal(inviteeId, added.UserId);
        Assert.Equal(ProjectId, added.ProjectId);
        Assert.Equal(inviter.UserId, added.InvitedByUserId);
        Assert.Equal(ProjectMembershipRole.ReadOnly, added.Role);
        Assert.True(added.IsActive);
        Assert.Equal(new InviteMemberResponse(added.ProjectMembershipId, inviteeId, ProjectId, "ReadOnly", true, added.CreatedAt, added.UpdatedAt), result);
    }

    [Fact]
    public async Task InviteMemberAsync_NonMember_DoesNotPersist()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.InviteMemberAsync(Guid.NewGuid(), Guid.NewGuid(), ProjectId));
        Assert.Empty(_repository.AddedMemberships);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task InviteMemberAsync_InsufficientRole_DoesNotPersist(ProjectMembershipRole role)
    {
        var inviter = AddMember(role);
        await Assert.ThrowsAsync<ValidationException>(() => _service.InviteMemberAsync(inviter.UserId, Guid.NewGuid(), ProjectId));
        Assert.Empty(_repository.AddedMemberships);
    }

    [Fact]
    public async Task InviteMemberAsync_InactiveInviter_DoesNotPersist()
    {
        var inviter = AddMember(ProjectMembershipRole.Admin);
        inviter.Deactivate(_creation.Membership);
        await Assert.ThrowsAsync<ValidationException>(() => _service.InviteMemberAsync(inviter.UserId, Guid.NewGuid(), ProjectId));
        Assert.Empty(_repository.AddedMemberships);
    }

    [Fact]
    public async Task InviteMemberAsync_EmptyInviteeId_DoesNotPersist()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.InviteMemberAsync(OwnerId, Guid.Empty, ProjectId));
        Assert.Empty(_repository.AddedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_MissingMembership_DoesNotPersist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateMemberRoleAsync(OwnerId, Guid.NewGuid(), ProjectId, ProjectMembershipRole.Admin));
        Assert.Single(_repository.MembershipLookups);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_NonMemberModifier_DoesNotPersist()
    {
        var member = AddMember(ProjectMembershipRole.ReadOnly);
        // The current service resolves its membershipId argument using a user-ID repository lookup.
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateMemberRoleAsync(Guid.NewGuid(), member.UserId, ProjectId, ProjectMembershipRole.Admin));
        Assert.Equal(ProjectMembershipRole.ReadOnly, member.Role);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_AuthorizedModifier_PersistsRoleAndMapsResponse()
    {
        var member = AddMember(ProjectMembershipRole.ReadOnly);

        var result = await _service.UpdateMemberRoleAsync(OwnerId, member.UserId, ProjectId, ProjectMembershipRole.Developer);

        Assert.Same(member, Assert.Single(_repository.UpdatedMemberships));
        Assert.Equal(ProjectMembershipRole.Developer, member.Role);
        Assert.Equal(OwnerId, member.LastModifiedByUserId);
        Assert.Equal(new UpdateMemberRoleResponse(member.ProjectMembershipId, member.UserId, ProjectId, "Developer", true, member.CreatedAt, member.UpdatedAt), result);
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task UpdateMemberRoleAsync_InsufficientModifierRole_DoesNotPersist(ProjectMembershipRole role)
    {
        var modifier = AddMember(role);
        var member = AddMember(ProjectMembershipRole.ReadOnly);

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateMemberRoleAsync(modifier.UserId, member.UserId, ProjectId, ProjectMembershipRole.Admin));

        Assert.Equal(ProjectMembershipRole.ReadOnly, member.Role);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_InvalidRole_DoesNotPersist()
    {
        var member = AddMember(ProjectMembershipRole.ReadOnly);
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateMemberRoleAsync(OwnerId, member.UserId, ProjectId, (ProjectMembershipRole)999));
        Assert.Equal(ProjectMembershipRole.ReadOnly, member.Role);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_InactiveMembership_DoesNotPersist()
    {
        var member = AddMember(ProjectMembershipRole.ReadOnly);
        member.Deactivate(_creation.Membership);

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateMemberRoleAsync(OwnerId, member.UserId, ProjectId, ProjectMembershipRole.Admin));

        Assert.Equal(ProjectMembershipRole.ReadOnly, member.Role);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_DemotingOwner_DoesNotPersist()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateMemberRoleAsync(OwnerId, OwnerId, ProjectId, ProjectMembershipRole.Admin));

        Assert.Equal(ProjectMembershipRole.Owner, _creation.Membership.Role);
        Assert.Empty(_repository.UpdatedMemberships);
    }

    private ProjectMembership AddMember(ProjectMembershipRole role)
    {
        if (role == ProjectMembershipRole.Owner) return _creation.Membership;
        var member = _creation.Membership.Invite(Guid.NewGuid(), role);
        _repository.Memberships.Add(member);
        return member;
    }

    private Task MutateProject(string operation, Guid userId) => operation switch
    {
        "Update" => _service.UpdateProjectAsync(ProjectId, userId, "Renamed"),
        "Delete" => _service.DeleteProjectAsync(ProjectId, userId),
        "Activate" => _service.ActivateProjectAsync(ProjectId, userId),
        "Deactivate" => _service.DeactivateProjectAsync(ProjectId, userId),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private void AssertNoProjectWrites()
    {
        Assert.Empty(_repository.UpdatedProjects);
        Assert.Empty(_repository.DeletedProjects);
    }

    // Records writes separately from seeded data so rejected operations cannot silently persist changes.
    internal sealed class ProjectRepositoryStub : IProjectRepository
    {
        public List<Project> Projects { get; } = [];
        public List<ProjectMembership> Memberships { get; } = [];
        public List<ProjectWithUserRole> UserProjects { get; set; } = [];
        public List<Guid> ProjectLookups { get; } = [];
        public List<Guid> UserLookups { get; } = [];
        public List<(Guid UserId, Guid ProjectId)> MembershipLookups { get; } = [];
        public List<ProjectCreationResponse> AddedProjects { get; } = [];
        public List<Project> UpdatedProjects { get; } = [];
        public List<Guid> DeletedProjects { get; } = [];
        public List<ProjectMembership> AddedMemberships { get; } = [];
        public List<ProjectMembership> UpdatedMemberships { get; } = [];

        public Task<Project?> GetByIdAsync(Guid projectId)
        {
            ProjectLookups.Add(projectId);
            return Task.FromResult(Projects.SingleOrDefault(p => p.ProjectId == projectId));
        }

        public Task<IEnumerable<ProjectWithUserRole>> GetByUserIdAsync(Guid userId)
        {
            UserLookups.Add(userId);
            return Task.FromResult<IEnumerable<ProjectWithUserRole>>(UserProjects);
        }

        public Task<ProjectMembership?> GetMembershipAsync(Guid userId, Guid projectId)
        {
            MembershipLookups.Add((userId, projectId));
            var project = Projects.SingleOrDefault(p => p.ProjectId == projectId);
            var membership = Memberships.SingleOrDefault(m => m.UserId == userId && m.ProjectId == projectId);
            if (project == null || membership == null)
                return Task.FromResult<ProjectMembership?>(null);

            // Match the real repository's Include(m => m.Project), including its
            // required relationship: a missing project cannot yield a membership.
            typeof(ProjectMembership).GetProperty(nameof(ProjectMembership.Project))!
                .SetValue(membership, project);
            return Task.FromResult<ProjectMembership?>(membership);
        }

        public Task<Project> AddAsync(ProjectCreationResponse creation)
        {
            AddedProjects.Add(creation);
            return Task.FromResult(creation.Project);
        }

        public Task<Project> UpdateAsync(Project project)
        {
            UpdatedProjects.Add(project);
            return Task.FromResult(project);
        }

        public Task<bool> DeleteAsync(Guid projectId)
        {
            DeletedProjects.Add(projectId);
            return Task.FromResult(true);
        }

        public Task<ProjectMembership> AddMembershipAsync(ProjectMembership membership)
        {
            AddedMemberships.Add(membership);
            return Task.FromResult(membership);
        }

        public Task<ProjectMembership> UpdateMembershipAsync(ProjectMembership membership)
        {
            UpdatedMemberships.Add(membership);
            return Task.FromResult(membership);
        }

        public Task<bool> DeleteMembershipAsync(Guid membershipId) => throw new NotSupportedException();
        public Task SaveChangesAsync() => throw new NotSupportedException();

        public List<ApiKey> ApiKeys { get; } = [];
        public List<ApiKey> AddedApiKeys { get; } = [];
        public Task<ApiKey?> GetApiKeyAsync(Guid apiKeyId) =>
            Task.FromResult(ApiKeys.SingleOrDefault(k => k.ApiKeyId == apiKeyId));
        public Task<ApiKey> AddApiKeyAsync(ApiKey apiKey)
        {
            ApiKeys.Add(apiKey);
            AddedApiKeys.Add(apiKey);
            return Task.FromResult(apiKey);
        }
        public Task<ApiKey> UpdateApiKeyAsync(ApiKey apiKey)
        {
            ApiKeys.RemoveAll(k => k.ApiKeyId == apiKey.ApiKeyId);
            ApiKeys.Add(apiKey);
            return Task.FromResult(apiKey);
        }
        public Task<bool> DeleteApiKeyAsync(Guid apiKeyId) =>
            Task.FromResult(ApiKeys.RemoveAll(k => k.ApiKeyId == apiKeyId) > 0);
        public Task<IEnumerable<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IEnumerable<ApiKey>>(ApiKeys.Where(k => k.ProjectId == projectId).ToArray());
        public Task<ApiKey?> GetApiKeyByPrefixAsync(Guid projectId, string keyPrefix) =>
            Task.FromResult(ApiKeys.SingleOrDefault(k => k.ProjectId == projectId && k.KeyPrefix == keyPrefix));
    }
}
