using EventFlow.Application.Interfaces;
using EventFlow.Application.Notifications;
using EventFlow.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Infrastructure.Services;

internal sealed class ProjectInvitationEmailHandler(
    IProjectRepository projects, IEmailSender email, IConfiguration configuration, TimeProvider time)
    : INotificationHandler<ProjectInvitationCreated>
{
    public async Task HandleAsync(ProjectInvitationCreated notification, CancellationToken cancellationToken)
    {
        var invitation = await projects.GetInvitationAsync(notification.InvitationId);
        if (invitation is null || invitation.StatusAt(time.GetUtcNow().UtcDateTime) != "Pending")
            return;

        var inviter = await projects.GetMembershipAsync(invitation.InviterUserId, invitation.ProjectId);
        if (inviter?.CanManageMemberships() != true ||
            (invitation.Role == ProjectMembershipRole.Admin && inviter.Role != ProjectMembershipRole.Owner))
            return;

        var configuredOrigin = configuration["ProjectInvitations:PublicAppUrl"];
        if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
            !(origin.Scheme == Uri.UriSchemeHttps || (origin.IsLoopback && origin.Scheme == Uri.UriSchemeHttp)) ||
            origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" ||
            origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new InvalidOperationException("ProjectInvitations:PublicAppUrl must be an HTTPS origin or a local HTTP origin.");

        var link = new Uri(origin, $"/invitations/accept?id={invitation.Id:D}");
        await email.SendAsync(invitation.Invitee.Email, "You've been invited to an EventFlow project", $"""
            You have been invited to join {invitation.Project.Name} as {invitation.Role}.

            Sign in with this EventFlow account and confirm the invitation:
            {link.AbsoluteUri}

            This invitation expires at {invitation.ExpiresAt:u}.
            """, cancellationToken);
    }
}
