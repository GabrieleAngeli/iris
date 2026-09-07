using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveOpenBaoIntegrationSettingsCommand(
    string Endpoint,
    string? Token,
    string MountPath,
    bool UseKvV2);

public sealed class SaveOpenBaoIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveOpenBaoIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        var mountPath = command.MountPath?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The OpenBao endpoint is required.");
        }

        if (mountPath.Length == 0)
        {
            throw new ValidationException("The OpenBao mount path is required.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        // A blank Token keeps whatever is already stored — a save that only changes the
        // mount path or KV version must not silently wipe a previously-saved token.
        var tokenReference = settings.OpenBaoTokenSecretReference;
        if (!string.IsNullOrEmpty(command.Token))
        {
            tokenReference = await secretStore
                .StoreAsync("openbao/token", command.Token, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.ConfigureOpenBao(endpoint, tokenReference, mountPath, command.UseKvV2);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: true,
            Message: "OpenBao settings saved. Restart Iris.Api for this instance to start using them.");
    }
}
