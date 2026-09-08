using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;
using Iris.Contracts.Setup;
using Iris.Domain.Settings;

namespace Iris.Application.Settings;

/// <summary>
/// Command for <c>PUT /system/settings/mail</c> — lets a platform.admin change the SMTP relay
/// after the setup wizard, not just once at first run. Unlike OpenBao/AWX/Ansible, this takes
/// effect immediately: <c>SmtpEmailSender</c> reads <see cref="IMailProviderSettingsRepository"/>
/// fresh on every send rather than caching it into a startup-only options singleton, so there is
/// no "restart required" story here at all.
/// </summary>
public sealed record SaveMailProviderSettingsCommand(MailProviderInput Mail);

public sealed class SaveMailProviderSettingsHandler(
    IMailProviderSettingsRepository mailSettings,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveMailProviderSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var mail = command.Mail ?? throw new ValidationException("Mail settings are required.");
        var smtpHost = mail.SmtpHost?.Trim() ?? string.Empty;
        var fromAddress = mail.FromAddress?.Trim() ?? string.Empty;

        if (smtpHost.Length == 0)
        {
            throw new ValidationException("SMTP host is required.");
        }

        if (mail.SmtpPort is <= 0 or > 65535)
        {
            throw new ValidationException("Enter a valid SMTP port (1-65535).");
        }

        if (fromAddress.Length == 0)
        {
            throw new ValidationException("A \"from\" address is required.");
        }

        // A blank password keeps whatever is already stored — same rule as the OpenBao/AWX
        // token fields, so an edit that only touches (say) the "from" address doesn't wipe the
        // SMTP password.
        var existing = await mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var passwordReference = existing?.SmtpPasswordSecretReference;
        if (!string.IsNullOrEmpty(mail.SmtpPassword))
        {
            passwordReference = await secretStore
                .StoreAsync("mail/smtp", mail.SmtpPassword, cancellationToken)
                .ConfigureAwait(false);
        }

        var settings = MailProviderSettings.Configure(
            smtpHost, mail.SmtpPort, mail.SmtpUsername, passwordReference,
            fromAddress, mail.FromDisplayName, mail.EnableSsl);
        await mailSettings.UpsertAsync(settings, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(RestartRequired: false, Message: "SMTP settings saved.");
    }
}
