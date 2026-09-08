using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveAzureDevOpsIntegrationSettingsCommand(
    string Endpoint,
    string? Token);

public sealed class SaveAzureDevOpsIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveAzureDevOpsIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The Azure DevOps organization URL is required.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        // Same rule as OpenBao/AWX: a blank Token keeps whatever is already stored.
        var tokenReference = settings.AzureDevOpsTokenSecretReference;
        if (!string.IsNullOrEmpty(command.Token))
        {
            tokenReference = await secretStore
                .StoreAsync("azure-devops/token", command.Token, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.ConfigureAzureDevOps(endpoint, tokenReference);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: true,
            Message: "Azure DevOps settings saved. Restart Iris.Api for this instance to start using them.");
    }
}
