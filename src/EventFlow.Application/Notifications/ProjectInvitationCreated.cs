using EventFlow.Application.Interfaces;

namespace EventFlow.Application.Notifications;

public sealed record ProjectInvitationCreated(Guid InvitationId) : INotification
{
    public static string Name => "project-invitation-created.v1";
}
