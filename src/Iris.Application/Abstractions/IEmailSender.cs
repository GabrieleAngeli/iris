namespace Iris.Application.Abstractions;

/// <summary>A single email to send. <see cref="IsHtml"/> controls how <see cref="Body"/> is interpreted.</summary>
public sealed record EmailMessage(string To, string Subject, string Body, bool IsHtml = false);

/// <summary>
/// SMTP settings to try — not necessarily saved yet (the setup wizard tests before persisting).
/// </summary>
public sealed record MailConnectionTestRequest(
    string SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string FromAddress,
    string? FromDisplayName,
    bool EnableSsl,
    string TestRecipient);

/// <summary>Which phase of <see cref="IEmailSender.TestConnectionAsync"/> failed.</summary>
public enum MailTestStage
{
    /// <summary>Could not reach the host on that port, or the TCP/TLS handshake failed.</summary>
    Connect,

    /// <summary>Reached the server, but the given credentials were rejected.</summary>
    Authenticate,

    /// <summary>Authenticated (or no auth needed), but the server rejected the test message.</summary>
    Send,
}

/// <summary>Raised by <see cref="IEmailSender.TestConnectionAsync"/>; <see cref="Stage"/> says which phase failed.</summary>
public sealed class MailConnectionException(MailTestStage stage, string message) : Exception(message)
{
    public MailTestStage Stage { get; } = stage;
}

/// <summary>
/// Sends real email through whatever provider <c>MailProviderSettings</c> currently holds.
/// Assumes the caller already checked one is configured — <see cref="SendAsync"/> throws if not.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects, authenticates (if credentials are given) and sends a real test email — against
    /// settings that may not be saved yet. Throws <see cref="MailConnectionException"/>, staged,
    /// on any failure; never partially "succeeds".
    /// </summary>
    Task TestConnectionAsync(MailConnectionTestRequest request, CancellationToken cancellationToken = default);
}
