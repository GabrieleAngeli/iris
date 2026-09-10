using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Contracts.Setup;
using Iris.Domain.Access;
using Iris.Domain.Settings;

namespace Iris.Application.Setup;

/// <summary>
/// Command for <c>POST /setup/complete</c> — the whole first-run wizard: configures OpenBao/AWX
/// (optional — "use existing" only, see <see cref="OpenBaoSetupInput"/>), the mail relay, and
/// creates the first super-admin, in one call. Anonymous, but only usable once — see the replay
/// guard in <see cref="HandleAsync"/>. <see cref="OpenBao"/>/<see cref="Awx"/> default to
/// <c>null</c> so existing callers that predate these two wizard steps keep compiling.
/// </summary>
public sealed record CompleteSetupCommand(
    MailProviderInput Mail,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword,
    OpenBaoSetupInput? OpenBao = null,
    AwxSetupInput? Awx = null);

public sealed class CompleteSetupHandler(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IUserRepository users,
    IMailProviderSettingsRepository mailSettings,
    ISecretStore secretStore,
    IEmailSender emailSender,
    IPasswordHasher passwordHasher,
    SessionIssuer sessionIssuer,
    IClock clock,
    IUnitOfWork unitOfWork,
    IIntegrationReachabilityProbe reachability,
    SaveOpenBaoIntegrationSettingsHandler saveOpenBao,
    SaveAwxIntegrationSettingsHandler saveAwx)
{
    public async Task<CompleteSetupResponse> HandleAsync(
        CompleteSetupCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var platformAdminRole = await roles.GetByKeyAsync(GetSetupStatusHandler.PlatformAdminRoleKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The built-in role catalog has not been seeded yet — this should never happen after startup.");

        // Re-checked here, not just by the client calling /setup/status first: an anonymous
        // endpoint must not be replayable into creating a second super-admin.
        if (await assignments.ExistsForRoleAsync(platformAdminRole.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException("Setup has already been completed.");
        }

        var mail = command.Mail;
        var email = command.AdminEmail?.Trim() ?? string.Empty;
        var displayName = command.AdminDisplayName?.Trim() ?? string.Empty;
        var password = command.AdminPassword ?? string.Empty;

        if (email.Length == 0)
        {
            throw new ValidationException("Administrator email is required.");
        }

        if (displayName.Length == 0)
        {
            throw new ValidationException("Administrator name is required.");
        }

        if (password.Length < SetMyPasswordHandler.MinimumLength)
        {
            throw new ValidationException($"The password must be at least {SetMyPasswordHandler.MinimumLength} characters.");
        }

        // Verified for real — reachability, connection, a genuine test send — before anything
        // about the mail settings (not even the secret) is persisted. The new admin's own
        // address is the test recipient: a working mail setup and a real confirmation email,
        // in one step, for someone who by definition can't have received an invitation for it.
        var testRequest = TestMailConnectionHandler.BuildTestRequest(mail, email);
        try
        {
            await emailSender.TestConnectionAsync(testRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (MailConnectionException ex)
        {
            throw new ValidationException(ex.Message);
        }

        string? passwordReference = null;
        if (!string.IsNullOrEmpty(mail.SmtpPassword))
        {
            passwordReference = await secretStore
                .StoreAsync("mail/smtp", mail.SmtpPassword, cancellationToken)
                .ConfigureAwait(false);
        }

        var settings = MailProviderSettings.Configure(
            mail.SmtpHost, mail.SmtpPort, mail.SmtpUsername, passwordReference,
            mail.FromAddress, mail.FromDisplayName, mail.EnableSsl);
        await mailSettings.UpsertAsync(settings, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        var externalId = SyntheticIdentity.DeriveObjectId(email);
        var admin = new User(Guid.CreateVersion7(), externalId, email, displayName);
        admin.SetPassword(passwordHasher.Hash(password), now);
        await users.AddAsync(admin, cancellationToken).ConfigureAwait(false);

        var assignment = new RoleAssignment(Guid.CreateVersion7(), admin.Id, platformAdminRole.Id, AccessScope.Global());
        await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

        var (token, expiresAtUtc) = await sessionIssuer.IssueAsync(admin.Id, cancellationToken).ConfigureAwait(false);

        // "Use existing" (endpoint given, not skipped, not asking Iris to provision it) is the
        // only mode handled here — it's just data persistence, safe to do from this anonymous,
        // one-shot endpoint. "Install for me" only sets the *ProvisionRequested flags below;
        // there is no anonymous provisioning endpoint to call — the client calls the
        // authenticated one (once it exists) after signing in with the token issued above.
        var openBaoProvisionRequested = command.OpenBao is { Skip: false, InstallForMe: true };
        if (command.OpenBao is { Skip: false, InstallForMe: false, Endpoint: { Length: > 0 } openBaoEndpoint })
        {
            // Same gate as the mail test above: reach it (and check the token, if given) before
            // persisting anything, so a wrong endpoint/token fails the wizard with a clear
            // message instead of being saved blind.
            try
            {
                await reachability.ProbeOpenBaoAsync(openBaoEndpoint, command.OpenBao.Token, cancellationToken).ConfigureAwait(false);
            }
            catch (IntegrationConnectionException ex)
            {
                throw new ValidationException($"OpenBao: {ex.Message}");
            }

            await saveOpenBao.HandleAsync(
                new SaveOpenBaoIntegrationSettingsCommand(openBaoEndpoint, command.OpenBao.Token, "secret", UseKvV2: true),
                cancellationToken).ConfigureAwait(false);
        }

        var awxProvisionRequested = command.Awx is { Skip: false, InstallForMe: true };
        if (command.Awx is { Skip: false, InstallForMe: false, Endpoint: { Length: > 0 } awxEndpoint })
        {
            AwxProbeResult awxProbe;
            try
            {
                awxProbe = await reachability.ProbeAwxAsync(
                    awxEndpoint,
                    command.Awx.Token,
                    command.Awx.OAuthClientId,
                    command.Awx.OAuthClientSecret,
                    command.Awx.RefreshToken,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (IntegrationConnectionException ex)
            {
                throw new ValidationException($"AWX: {ex.Message}");
            }

            // When OAuth2 refresh was validated, the probe already did the first refresh — AWX
            // rotated the refresh token, so persist the fresh pair it returned, not what was typed.
            await saveAwx.HandleAsync(
                new SaveAwxIntegrationSettingsCommand(
                    awxEndpoint,
                    awxProbe.RefreshedAccessToken ?? command.Awx.Token,
                    command.Awx.JobTemplateId,
                    command.Awx.OAuthClientId,
                    command.Awx.OAuthClientSecret,
                    awxProbe.RefreshedRefreshToken ?? command.Awx.RefreshToken),
                cancellationToken).ConfigureAwait(false);
        }

        // The two SaveXxxIntegrationSettingsHandler calls above already flush every change
        // pending on this same tracked context (admin/mail settings included) via their own
        // SaveChangesAsync — this call is what actually persists everything in the common case
        // where neither OpenBao nor AWX "use existing" was selected.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CompleteSetupResponse(admin.Id, admin.Email, token, expiresAtUtc, openBaoProvisionRequested, awxProvisionRequested);
    }
}
