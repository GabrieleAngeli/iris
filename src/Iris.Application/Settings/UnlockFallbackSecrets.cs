using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;

namespace Iris.Application.Settings;

/// <summary>
/// Command for <c>POST /system/settings/secrets/unlock</c> — the only way any fallback-store
/// secret (OpenBao's own token, AWX token, SMTP password, or anything else saved before OpenBao
/// was configured) ever becomes durable across an Iris.Api restart, or comes back after one. See
/// <c>IFallbackSecretVault</c>/<c>EncryptedSecretEntry</c> for the full design — this handler is
/// only the password-verification gate in front of it.
/// </summary>
public sealed record UnlockFallbackSecretsCommand(string Password);

public sealed class UnlockFallbackSecretsHandler(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IPasswordHasher passwordHasher,
    IFallbackSecretVault vault)
{
    public async Task<FallbackSecretUnlockResult> HandleAsync(
        UnlockFallbackSecretsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Resolved the same way SetMyPasswordHandler does (see ManageMyPassword.cs) — by
        // ExternalId via provisioning, not by reading the "iris:uid" claim directly. That claim
        // is only stamped once AccessProvisioningClaimsTransformation has run for this exact
        // principal; relying on ICurrentUser.UserId here intermittently saw it unset for a
        // dev-header+password authenticated request even though the user unambiguously exists —
        // a real bug caught by FallbackSecretVaultApiTests's end-to-end test, not a hypothetical.
        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);

        // An SSO-only admin who never set a local password has nothing to derive a key from —
        // real edge case, not a made-up one: HasPassword already gates the local-login flow
        // (see LoginHandler) for exactly this reason.
        if (!user.HasPassword)
        {
            throw new ValidationException(
                "Set a local password for your account first (Profile > Change password) before you can unlock fallback secrets.");
        }

        if (string.IsNullOrEmpty(command.Password) || !passwordHasher.Verify(command.Password, user.PasswordHash!))
        {
            throw new ValidationException("Incorrect password.");
        }

        return await vault.UnlockAsync(user.Id, command.Password, cancellationToken).ConfigureAwait(false);
    }
}
