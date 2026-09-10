using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

/// <summary>
/// Command for <c>POST /system/integrations/openbao/promote</c>. Migrates every secret currently
/// in the encrypted fallback vault into the configured OpenBao instance and makes OpenBao the
/// live <c>ISecretStore</c> — no Iris.Api restart. Needs OpenBao's token to already be resolvable
/// (i.e. the fallback vault unlocked, which now happens automatically on login).
/// </summary>
public sealed record PromoteSecretStoreToOpenBaoCommand;

public sealed class PromoteSecretStoreToOpenBaoHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    ISecretStorePromotion promotion)
{
    public async Task<PromoteSecretStoreResponse> HandleAsync(
        PromoteSecretStoreToOpenBaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (promotion.IsOpenBaoActive)
        {
            return new PromoteSecretStoreResponse(false, 0, "OpenBao is already the active secret store.");
        }

        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || string.IsNullOrWhiteSpace(settings.OpenBaoEndpoint))
        {
            throw new ValidationException("Configure OpenBao (endpoint + token) before promoting it to the secret store.");
        }

        if (string.IsNullOrWhiteSpace(settings.OpenBaoTokenSecretReference))
        {
            throw new ValidationException("No OpenBao token is stored — set it in the Configure OpenBao dialog first.");
        }

        var token = await secretStore.RetrieveAsync(settings.OpenBaoTokenSecretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationException(
                "The OpenBao token isn't available yet — unlock the fallback secrets first (System settings), or set the token in configuration.");
        }

        int migrated;
        try
        {
            migrated = await promotion
                .PromoteToOpenBaoAsync(settings.OpenBaoEndpoint!, token, settings.OpenBaoMountPath, settings.OpenBaoUseKvV2, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SecretStorePromotionException ex)
        {
            throw new ValidationException(ex.Message);
        }

        return new PromoteSecretStoreResponse(
            true,
            migrated,
            $"Migrated {migrated} secret(s) into OpenBao; it is now the active secret store.");
    }
}
