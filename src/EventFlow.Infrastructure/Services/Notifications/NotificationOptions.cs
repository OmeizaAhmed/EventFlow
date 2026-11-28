namespace EventFlow.Infrastructure.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool ImplicitTls { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
