using System.Net;
using System.Net.Http.Json;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests
{
    [Fact]
    public async Task Members_InviteChangeRoleDeactivateAndReactivate()
    {
        var owner = await RegisterAndLoginAsync();
        var invitee = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var url = $"/api/projects/{project.ProjectId}/members";

        using var invite = await owner.Client.PostAsJsonAsync(url, new { Email = invitee.Email.ToUpperInvariant() });
        await AssertStatusAsync(HttpStatusCode.Created, invite);
        var pending = await ReadAsync<ProjectInvitationResponse>(invite);
        Assert.Equal("ReadOnly", pending.Role);
        Assert.Equal("Pending", pending.Status);
        var invitationUrl = $"/api/project-invitations/{pending.InvitationId}";
        Assert.EndsWith(invitationUrl, invite.Headers.Location!.ToString());

        using var duplicate = await owner.Client.PostAsJsonAsync(url, new { InviteeId = invitee.UserId });
        await AssertStatusAsync(HttpStatusCode.Conflict, duplicate);
        Assert.Empty((await invitee.Client.GetFromJsonAsync<ProjectResponse[]>("/api/projects"))!);
        using var wrongAccount = await owner.Client.PostAsync($"{invitationUrl}/accept", null);
        await AssertStatusAsync(HttpStatusCode.NotFound, wrongAccount);
        using var details = await invitee.Client.GetAsync(invitationUrl);
        await AssertStatusAsync(HttpStatusCode.OK, details);
        Assert.Equal("Pending", (await ReadAsync<ProjectInvitationResponse>(details)).Status);

        var member = await AcceptInvitationAsync(invitee.Client, pending.InvitationId);
        var memberUrl = $"{url}/{member.ProjectMembershipId}";
        var repeated = await AcceptInvitationAsync(invitee.Client, pending.InvitationId);
        Assert.Equal(member.ProjectMembershipId, repeated.ProjectMembershipId);
        using var listed = await invitee.Client.GetAsync(url);
        await AssertStatusAsync(HttpStatusCode.OK, listed);
        var roster = (await ReadAsync<ProjectMembersResponse>(listed)).Members;
        Assert.Equal(2, roster.Count);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(roster, row => row.UserId == invitee.UserId).FirstName));

        using var forbidden = await invitee.Client.PutAsJsonAsync($"{memberUrl}/role", new { Role = "Admin" });
        await AssertStatusAsync(HttpStatusCode.Forbidden, forbidden);
        using var changed = await owner.Client.PutAsJsonAsync($"{memberUrl}/role", new { Role = "Developer" });
        await AssertStatusAsync(HttpStatusCode.OK, changed);
        Assert.Equal("Developer", (await ReadAsync<ProjectMemberResponse>(changed)).Role);

        using var deactivated = await owner.Client.PostAsync($"{memberUrl}/deactivate", null);
        await AssertStatusAsync(HttpStatusCode.OK, deactivated);
        Assert.False((await ReadAsync<ProjectMemberResponse>(deactivated)).IsActive);
        using var inactive = await invitee.Client.GetAsync(url);
        await AssertStatusAsync(HttpStatusCode.NotFound, inactive);
        Assert.Empty((await invitee.Client.GetFromJsonAsync<ProjectResponse[]>("/api/projects"))!);

        using var reactivated = await owner.Client.PostAsync($"{memberUrl}/activate", null);
        await AssertStatusAsync(HttpStatusCode.OK, reactivated);
        using var current = await invitee.Client.GetAsync(memberUrl);
        await AssertStatusAsync(HttpStatusCode.OK, current);
        Assert.True((await ReadAsync<ProjectMemberResponse>(current)).IsActive);
    }

    [Fact]
    public async Task Members_OnlyCurrentOwnerCanTransferToAnAdminWithinTheProject()
    {
        var owner = await RegisterAndLoginAsync();
        var admin = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var other = await CreateProjectAsync(owner.Client);
        var url = $"/api/projects/{project.ProjectId}/members";

        using var invite = await owner.Client.PostAsJsonAsync(url, new { InviteeId = admin.UserId, Role = "Admin" });
        await AssertStatusAsync(HttpStatusCode.Created, invite);
        var invitation = await ReadAsync<ProjectInvitationResponse>(invite);
        var member = await AcceptInvitationAsync(admin.Client, invitation.InvitationId);
        var target = $"{url}/{member.ProjectMembershipId}";
        using var promotion = await owner.Client.PutAsJsonAsync($"{target}/role", new { Role = "Owner" });
        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, promotion);
        using var crossProject = await owner.Client.PostAsync(
            $"/api/projects/{other.ProjectId}/members/{member.ProjectMembershipId}/transfer-ownership", null);
        await AssertStatusAsync(HttpStatusCode.NotFound, crossProject);
        using var forbidden = await admin.Client.PostAsync($"{target}/transfer-ownership", null);
        await AssertStatusAsync(HttpStatusCode.Forbidden, forbidden);

        using var transfer = await owner.Client.PostAsync($"{target}/transfer-ownership", null);
        await AssertStatusAsync(HttpStatusCode.OK, transfer);
        using var roster = await admin.Client.GetAsync(url);
        var members = (await ReadAsync<ProjectMembersResponse>(roster)).Members;
        Assert.Equal(admin.UserId, Assert.Single(members, row => row.Role == "Owner").UserId);
        var formerOwner = Assert.Single(members, row => row.UserId == owner.UserId);
        Assert.Equal("Admin", formerOwner.Role);
        Assert.True(formerOwner.IsActive);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("inviter-deactivated")]
    public async Task Invitations_RejectUnavailableInvitationsWithoutGrantingAccess(string caseName)
    {
        var owner = await RegisterAndLoginAsync();
        var admin = await RegisterAndLoginAsync();
        var invitee = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, admin.UserId, ProjectMembershipRole.Admin);

        var membersUrl = $"/api/projects/{project.ProjectId}/members";
        using var prohibited = await admin.Client.PostAsJsonAsync(membersUrl, new { InviteeId = invitee.UserId, Role = "Admin" });
        await AssertStatusAsync(HttpStatusCode.Forbidden, prohibited);
        using var invite = await admin.Client.PostAsJsonAsync(membersUrl, new { InviteeId = invitee.UserId, Role = "Developer" });
        await AssertStatusAsync(HttpStatusCode.Created, invite);
        var invitation = await ReadAsync<ProjectInvitationResponse>(invite);

        var invitationsUrl = $"/api/projects/{project.ProjectId}/invitations";
        var pending = await owner.Client.GetFromJsonAsync<ProjectInvitationResponse[]>(invitationsUrl);
        Assert.Equal(invitation.InvitationId, Assert.Single(pending!).InvitationId);
        using var forbidden = await invitee.Client.GetAsync(invitationsUrl);
        await AssertStatusAsync(HttpStatusCode.Forbidden, forbidden);

        if (caseName == "revoked")
        {
            using var revoked = await owner.Client.DeleteAsync($"{invitationsUrl}/{invitation.InvitationId}");
            await AssertStatusAsync(HttpStatusCode.NoContent, revoked);
        }
        else if (caseName == "expired")
        {
            await WithDatabaseAsync(db => db.Set<ProjectInvitation>().Where(row => row.Id == invitation.InvitationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        }
        else
        {
            var roster = await owner.Client.GetFromJsonAsync<ProjectMembersResponse>(membersUrl);
            var adminMembership = Assert.Single(roster!.Members, row => row.UserId == admin.UserId);
            using var deactivated = await owner.Client.PostAsync($"{membersUrl}/{adminMembership.ProjectMembershipId}/deactivate", null);
            await AssertStatusAsync(HttpStatusCode.OK, deactivated);
        }

        using var accepted = await invitee.Client.PostAsync($"/api/project-invitations/{invitation.InvitationId}/accept", null);
        await AssertStatusAsync(HttpStatusCode.Conflict, accepted);
        Assert.Empty((await invitee.Client.GetFromJsonAsync<ProjectResponse[]>("/api/projects"))!);
    }

    private static async Task<ProjectMemberResponse> AcceptInvitationAsync(HttpClient client, Guid invitationId)
    {
        using var response = await client.PostAsync($"/api/project-invitations/{invitationId}/accept", null);
        await AssertStatusAsync(HttpStatusCode.OK, response);
        return await ReadAsync<ProjectMemberResponse>(response);
    }
}
