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

        using var client = new SmtpClient();
        await ConnectAndAuthenticateAsync(
            client, settings.SmtpHost, settings.SmtpPort, settings.EnableSsl,
            settings.SmtpUsername, password, cancellationToken).ConfigureAwait(false);

        var mime = BuildMessage(settings.FromAddress, settings.FromDisplayName, message.To, message.Subject, message.Body, message.IsHtml);

        try
        {
            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisconnectQuietlyAsync(client, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task TestConnectionAsync(MailConnectionTestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var client = new SmtpClient();

        await ConnectAndAuthenticateAsync(
            client, request.SmtpHost, request.SmtpPort, request.EnableSsl,
            request.SmtpUsername, request.SmtpPassword, cancellationToken).ConfigureAwait(false);

        var mime = BuildMessage(
            request.FromAddress, request.FromDisplayName, request.TestRecipient,
            "Iris — mail settings test", "This is a test email from Iris confirming your SMTP settings work.", isHtml: false);

        try
        {
            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MailConnectionException(
                MailTestStage.Send,
                BuildSendFailureMessage(request.SmtpUsername, request.FromAddress, ex));
        }
        finally
        {
            await DisconnectQuietlyAsync(client, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reachability + the SMTP/TLS handshake, then authentication if credentials are given.</summary>
    private static async Task ConnectAndAuthenticateAsync(
        SmtpClient client,
        string host,
        int port,
        bool enableSsl,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        var secureOption = enableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;

        try
        {
            await client.ConnectAsync(host, port, secureOption, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MailConnectionException(MailTestStage.Connect, $"Could not reach {host}:{port} — {ex.Message}");
        }

        if (string.IsNullOrEmpty(username))
        {
            return;
        }

        try
        {
            await client.AuthenticateAsync(username, password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MailConnectionException(MailTestStage.Authenticate, $"Connected, but authentication failed: {ex.Message}");
        }
    }

    private static MimeMessage BuildMessage(
        string fromAddress, string? fromDisplayName, string to, string subject, string body, bool isHtml)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromDisplayName ?? fromAddress, fromAddress));
        mime.To.Add(MailboxAddress.Parse(to));
        mime.Subject = subject;
        mime.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };
        return mime;
    }

    private static string BuildSendFailureMessage(string? smtpUsername, string fromAddress, Exception ex)
    {
        if (IsSendAsDenied(ex.Message))
        {
            var account = string.IsNullOrWhiteSpace(smtpUsername)
                ? "The configured SMTP account"
                : $"The SMTP account '{smtpUsername}'";

            return $"{account} is not allowed to send as '{fromAddress}'. Use that same address as the From address, or grant Send As permission for it in Microsoft 365.";
        }

        return $"Connected, but sending the test email failed: {TrimMailServerDiagnostics(ex.Message)}";
    }

    private static bool IsSendAsDenied(string message) =>
        message.Contains("SendAsDenied", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("not allowed to send as", StringComparison.OrdinalIgnoreCase);

    private static string TrimMailServerDiagnostics(string message)
    {
        var marker = message.IndexOf("[BeginDiagnosticData]", StringComparison.OrdinalIgnoreCase);
        var trimmed = marker >= 0 ? message[..marker] : message;
        trimmed = trimmed.Trim();
        return trimmed.Length <= 300 ? trimmed : string.Concat(trimmed.AsSpan(0, 300), "...");
    }

    private static async Task DisconnectQuietlyAsync(SmtpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort — the send (or its failure) already happened either way.
        }
    }
}
