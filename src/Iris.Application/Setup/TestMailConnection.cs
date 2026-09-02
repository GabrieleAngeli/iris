using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Setup;

namespace Iris.Application.Setup;

/// <summary>
/// Command for <c>POST /setup/test-mail</c> — the wizard's "test connection" button: tries the
/// mail fields as typed, before anything is saved, by actually sending a real email.
/// </summary>
public sealed record TestMailConnectionCommand(MailProviderInput Mail, string TestRecipient);

public sealed class TestMailConnectionHandler(IEmailSender emailSender)
{
    public async Task HandleAsync(TestMailConnectionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = BuildTestRequest(command.Mail, command.TestRecipient);

        try
        {
            await emailSender.TestConnectionAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (MailConnectionException ex)
        {
            throw new ValidationException(ex.Message);
        }
    }

    /// <summary>Shared with <c>CompleteSetupHandler</c> so both validate/build the request the same way.</summary>
    internal static MailConnectionTestRequest BuildTestRequest(MailProviderInput? mail, string? testRecipient)
    {
        if (mail is null || string.IsNullOrWhiteSpace(mail.SmtpHost))
        {
            throw new ValidationException("SMTP host is required.");
        }

        if (mail.SmtpPort is <= 0 or > 65535)
        {
            throw new ValidationException("SMTP port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(mail.FromAddress))
        {
            throw new ValidationException("A \"from\" address is required.");
        }

        if (string.IsNullOrWhiteSpace(testRecipient))
        {
            throw new ValidationException("A recipient address is required to send a test email.");
        }

        return new MailConnectionTestRequest(
            mail.SmtpHost.Trim(),
            mail.SmtpPort,
            string.IsNullOrWhiteSpace(mail.SmtpUsername) ? null : mail.SmtpUsername.Trim(),
            string.IsNullOrEmpty(mail.SmtpPassword) ? null : mail.SmtpPassword,
            mail.FromAddress.Trim(),
            string.IsNullOrWhiteSpace(mail.FromDisplayName) ? null : mail.FromDisplayName.Trim(),
            mail.EnableSsl,
            testRecipient.Trim());
    }
}
