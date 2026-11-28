using EventFlow.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EventFlow.Infrastructure.Services;

internal sealed class SmtpEmailSender(IOptions<NotificationOptions> options) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port,
            smtp.ImplicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password!, cancellationToken);
        var result = await client.SendAsync(FormatOptions.Default, new SmtpSendRequest(message), cancellationToken);
        if (result.AcceptedRecipients.Count == 0)
            throw new InvalidOperationException("The SMTP server declined the notification recipient.");
    }
}
