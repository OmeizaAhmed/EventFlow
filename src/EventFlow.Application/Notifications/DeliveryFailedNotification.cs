using EventFlow.Application.Interfaces;

namespace EventFlow.Application.Notifications;

public sealed record DeliveryFailedNotification(Guid ProjectId, Guid DeliveryId, Guid EventId,
    int AttemptCount, int? LastHttpStatusCode) : INotification
{
    public static string Name => "webhook-delivery-failed.v1";
}

public sealed record DeliveryFailureRecipient(string Email, string ProjectName);

public sealed class DeliveryFailureEmailHandler(IProjectRepository projects, IEmailSender email)
    : INotificationHandler<DeliveryFailedNotification>
{
    public async Task HandleAsync(DeliveryFailedNotification notification, CancellationToken cancellationToken)
    {
        var recipients = await projects.GetDeliveryFailureRecipientsAsync(notification.ProjectId, cancellationToken);
        foreach (var recipient in recipients)
        {
            var status = notification.LastHttpStatusCode?.ToString() ?? "No HTTP response";
            await email.SendAsync(recipient.Email, "EventFlow webhook delivery failed", $"""
                A webhook delivery for {recipient.ProjectName} reached DeadLetter.

                Project ID: {notification.ProjectId}
                Delivery ID: {notification.DeliveryId}
                Event ID: {notification.EventId}
                Attempts: {notification.AttemptCount}
                Last HTTP status: {status}

                Inspect the delivery in EventFlow and replay it when the endpoint is ready.
                """, cancellationToken);
        }
    }
}
