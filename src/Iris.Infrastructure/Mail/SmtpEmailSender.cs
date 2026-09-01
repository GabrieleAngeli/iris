using Iris.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Iris.Infrastructure.Mail;

/// <summary>
/// Sends real email through the SMTP relay configured in the setup wizard. Resolves the
/// password from <see cref="ISecretStore"/> fresh on every send rather than caching it.
/// </summary>
internal sealed class SmtpEmailSender(IMailProviderSettingsRepository mailSettings, ISecretStore secretStore) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = await mailSettings.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Mail is not configured yet — run the setup wizard first.");

        string? password = null;
        if (settings.SmtpPasswordSecretReference is { Length: > 0 } reference)
        {
            password = await secretStore.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromDisplayName ?? settings.FromAddress, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };

        using var client = new SmtpClient();
        var secureOption = settings.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureOption, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(settings.SmtpUsername))
        {
            await client.AuthenticateAsync(settings.SmtpUsername, password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}
