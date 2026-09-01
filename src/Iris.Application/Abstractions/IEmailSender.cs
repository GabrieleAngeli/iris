namespace Iris.Application.Abstractions;

/// <summary>A single email to send. <see cref="IsHtml"/> controls how <see cref="Body"/> is interpreted.</summary>
public sealed record EmailMessage(string To, string Subject, string Body, bool IsHtml = false);

/// <summary>
/// Sends real email through whatever provider <c>MailProviderSettings</c> currently holds.
/// Assumes the caller already checked one is configured — <see cref="SendAsync"/> throws if not.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
