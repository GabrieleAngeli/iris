using Iris.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Iris.Infrastructure.Invitations;

/// <summary>
/// Delivers an invitation by real email through the SMTP relay configured in the setup wizard.
/// Before that wizard has run — or if it hasn't reached this deployment yet — there is nowhere
/// to send from, so this falls back to writing the link to the log (the API also returns the
/// link in the response, so nothing is lost either way).
/// </summary>
internal sealed class SmtpInvitationNotifier(
    IMailProviderSettingsRepository mailSettings,
    IEmailSender emailSender,
    ILogger<SmtpInvitationNotifier> logger) : IInvitationNotifier
{
    public async Task SendAsync(InvitationNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var settings = await mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            logger.LogInformation(
                "Invitation for {DisplayName} <{Email}> (expires {ExpiresAtUtc:u}): {AcceptLink}",
                notification.DisplayName,
                notification.Email,
                notification.ExpiresAtUtc,
                notification.AcceptLink);
            return;
        }

        var body =
            $"Hello {notification.DisplayName},\n\n" +
            "You've been invited to Iris. Open the Iris application, go to \"Set your password from an " +
            "invitation\" on the sign-in screen, and paste this link (or just the token) there to set your " +
            $"password — it isn't a web page, opening it in a browser won't work:\n{notification.AcceptLink}\n\n" +
            $"This link expires {notification.ExpiresAtUtc:u}.";

        await emailSender
            .SendAsync(new EmailMessage(notification.Email, "You're invited to Iris", body), cancellationToken)
            .ConfigureAwait(false);
    }
}
