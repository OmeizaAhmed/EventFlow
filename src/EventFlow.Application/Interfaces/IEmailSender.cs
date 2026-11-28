namespace EventFlow.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken);
}
