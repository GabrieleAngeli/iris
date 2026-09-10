using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveAwxIntegrationSettingsCommand(
    string Endpoint,
    string? Token,
    int? JobTemplateId,
    string? OAuthClientId = null,
    string? OAuthClientSecret = null,
    string? RefreshToken = null);

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

        // Same rule as OpenBao for every secret here: a blank value keeps whatever is already
        // stored, it does not clear it.
        var tokenReference = await KeepOrStoreAsync(
            command.Token, "awx/token", settings.AwxTokenSecretReference, cancellationToken).ConfigureAwait(false);
        var clientSecretReference = await KeepOrStoreAsync(
            command.OAuthClientSecret, "awx/oauth-client-secret", settings.AwxOAuthClientSecretReference, cancellationToken).ConfigureAwait(false);
        var refreshTokenReference = await KeepOrStoreAsync(
            command.RefreshToken, "awx/refresh-token", settings.AwxRefreshTokenSecretReference, cancellationToken).ConfigureAwait(false);

        var clientId = string.IsNullOrWhiteSpace(command.OAuthClientId)
            ? settings.AwxOAuthClientId
            : command.OAuthClientId.Trim();

        // Same partial-update rule as the secrets: a blank job template id keeps the stored one
        // rather than wiping it (the Configure dialog is a partial form — an empty field means
        // "leave it", not "clear it").
        var jobTemplateId = command.JobTemplateId ?? settings.AwxJobTemplateId;

        settings.ConfigureAwx(endpoint, tokenReference, jobTemplateId, clientId, clientSecretReference, refreshTokenReference);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: true,
            Message: "AWX settings saved. Restart Iris.Api for this instance to start using them.");
    }

    private async Task<string?> KeepOrStoreAsync(
        string? value, string logicalPath, string? existingReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(value))
        {
            return existingReference;
        }

        return await secretStore.StoreAsync(logicalPath, value, cancellationToken).ConfigureAwait(false);
    }
}
