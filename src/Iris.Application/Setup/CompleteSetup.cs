using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Contracts.Setup;
using Iris.Domain.Access;
using Iris.Domain.Settings;

namespace Iris.Application.Setup;

/// <summary>
/// Command for <c>POST /setup/complete</c> — the whole first-run wizard: configures the mail
/// relay and creates the first super-admin, in one call. Anonymous, but only usable once — see
/// the replay guard in <see cref="HandleAsync"/>.
/// </summary>
public sealed record CompleteSetupCommand(
    MailProviderInput Mail,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword);

public sealed class CompleteSetupHandler(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IUserRepository users,
    IMailProviderSettingsRepository mailSettings,
    ISecretStore secretStore,
    IPasswordHasher passwordHasher,
    SessionIssuer sessionIssuer,
    IClock clock,
    IUnitOfWork unitOfWork)
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

        var mail = command.Mail ?? throw new ValidationException("Mail provider settings are required.");
        if (string.IsNullOrWhiteSpace(mail.SmtpHost))
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

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CompleteSetupResponse(admin.Id, admin.Email, token, expiresAtUtc);
    }
}
