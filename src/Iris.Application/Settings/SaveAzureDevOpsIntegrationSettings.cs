using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveAzureDevOpsIntegrationSettingsCommand(
    string Endpoint,
    string? Token,
    string? Project = null,
    string? Repository = null,
    string? Branch = null,
    string? ManifestPath = null);

public sealed class SaveAzureDevOpsIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork,
    IIntegrationSettingsReloader reloader)
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

        // Same partial-update rule as AWX's job template ids: a blank field on a re-save keeps
        // whatever is already stored rather than wiping it — the Configure dialog is a partial
        // form, not a full replace.
        var project = string.IsNullOrWhiteSpace(command.Project) ? settings.AzureDevOpsProject : command.Project;
        var repository = string.IsNullOrWhiteSpace(command.Repository) ? settings.AzureDevOpsRepository : command.Repository;
        var branch = string.IsNullOrWhiteSpace(command.Branch) ? settings.AzureDevOpsBranch : command.Branch;
        var manifestPath = string.IsNullOrWhiteSpace(command.ManifestPath) ? settings.AzureDevOpsManifestPath : command.ManifestPath;

        settings.ConfigureAzureDevOps(endpoint, tokenReference, project, repository, branch, manifestPath);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await reloader.ReloadAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: false,
            Message: "Azure DevOps settings saved and active immediately.");
    }
}
