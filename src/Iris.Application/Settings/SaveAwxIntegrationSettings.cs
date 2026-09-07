using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveAwxIntegrationSettingsCommand(
    string Endpoint,
    string? Token,
    int? JobTemplateId);

public sealed class SaveAwxIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveAwxIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The AWX endpoint is required.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        // Same rule as OpenBao: a blank Token keeps whatever is already stored.
        var tokenReference = settings.AwxTokenSecretReference;
        if (!string.IsNullOrEmpty(command.Token))
        {
            tokenReference = await secretStore
                .StoreAsync("awx/token", command.Token, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.ConfigureAwx(endpoint, tokenReference, command.JobTemplateId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: true,
            Message: "AWX settings saved. Restart Iris.Api for this instance to start using them.");
    }
}
