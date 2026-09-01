using Iris.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Iris.Infrastructure.Invitations;

/// <summary>
/// Stand-in for a real delivery channel (SMTP, Microsoft Graph B2B invite): writes the
/// invitation link to the log so a developer can copy it. The API also returns the link
/// in the response, so nothing is lost when this is the only channel.
/// </summary>
internal sealed class LoggingInvitationNotifier(ILogger<LoggingInvitationNotifier> logger) : IInvitationNotifier
{
    public Task SendAsync(InvitationNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(
            "Invitation for {DisplayName} <{Email}> (expires {ExpiresAtUtc:u}): {AcceptLink}",
            notification.DisplayName,
            notification.Email,
            notification.ExpiresAtUtc,
            notification.AcceptLink);

        return Task.CompletedTask;
    }
}
