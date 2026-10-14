using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;
using EventFlow.Infrastructure.Services;

namespace EventFlow.Tests;

public partial class ProjectServiceTests
{
    [Theory]
    [InlineData(ProjectMembershipRole.Owner, true)]
    [InlineData(ProjectMembershipRole.Owner, false)]
    [InlineData(ProjectMembershipRole.Admin, true)]
    [InlineData(ProjectMembershipRole.Admin, false)]
    public async Task GenerateApiKeyAsync_AuthorizedRole_PersistsHashAndReturnsSecret(ProjectMembershipRole role, bool isLive)
    {
        var member = AddMember(role);
        var result = await _service.GenerateApiKeyAsync(ProjectId, member.UserId, isLive);

        var stored = Assert.Single(_repository.AddedApiKeys);
        Assert.Same(stored, Assert.Single(_creation.Project.ApiKeys));
        Assert.NotEqual(Guid.Empty, result.ApiKeyId);
        Assert.Equal(stored.ApiKeyId, result.ApiKeyId);
        Assert.Equal(ProjectId, result.ProjectId);
        Assert.Equal(stored.KeyPrefix, result.KeyPrefix);
        Assert.Equal(stored.CreatedAt, result.CreatedAt);
        Assert.Equal(isLive, stored.IsLive);
        Assert.Equal(isLive, result.IsLive);
        Assert.Null(stored.RevokedAt);
        Assert.NotEqual(result.plainTextKey, stored.HashedKey);
        Assert.True(new ApiKeyService().Verify(result.plainTextKey, stored.HashedKey));
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Developer)]
    [InlineData(ProjectMembershipRole.ReadOnly)]
    public async Task GenerateApiKeyAsync_InsufficientRole_DoesNotPersist(ProjectMembershipRole role)
    {
        var member = AddMember(role);
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GenerateApiKeyAsync(ProjectId, member.UserId, false));
        Assert.Empty(_repository.AddedApiKeys);
        Assert.Empty(_creation.Project.ApiKeys);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_NonMember_DoesNotPersist()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GenerateApiKeyAsync(ProjectId, Guid.NewGuid(), false));
        Assert.Empty(_repository.AddedApiKeys);
        Assert.Empty(_repository.ProjectLookups);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_MissingProject_DoesNotPersist()
    {
        _repository.Projects.Clear();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GenerateApiKeyAsync(ProjectId, OwnerId, false));
        Assert.Empty(_repository.AddedApiKeys);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_InactiveProject_DoesNotPersist()
    {
        _creation.Project.Deactivate();
        await Assert.ThrowsAsync<ValidationException>(() => _service.GenerateApiKeyAsync(ProjectId, OwnerId, false));
        Assert.Empty(_repository.AddedApiKeys);
        Assert.Empty(_creation.Project.ApiKeys);
    }
}
