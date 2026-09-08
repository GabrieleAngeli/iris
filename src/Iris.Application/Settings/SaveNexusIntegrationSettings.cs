using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveNexusIntegrationSettingsCommand(
    string Endpoint,
    string? Token);

public sealed class SaveNexusIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveNexusIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The Nexus repository URL is required.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        // Same rule as OpenBao/AWX: a blank Token keeps whatever is already stored. Not required
        // for the reachability check itself (Nexus's status endpoint is anonymous), but stored
        // for when real artifact operations need it later.
        var tokenReference = settings.NexusTokenSecretReference;
        if (!string.IsNullOrEmpty(command.Token))
        {
            tokenReference = await secretStore
                .StoreAsync("nexus/token", command.Token, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.ConfigureNexus(endpoint, tokenReference);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: true,
            Message: "Nexus settings saved. Restart Iris.Api for this instance to start using them.");
    }
}
