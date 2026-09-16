using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Settings;

/// <summary>Command for <c>PUT /system/integrations/ops-host</c>. <c>Secret</c> left empty/null
/// keeps whatever secret reference is already stored — same rule as every other credential save
/// in this codebase. <c>AuthMethod</c> is "Password" or "SshKey" (same convention as
/// <c>ServerCredentialFactory</c>'s request shape).</summary>
public sealed record SaveOpsHostIntegrationSettingsCommand(
    string Endpoint,
    int Port,
    string Username,
    string AuthMethod,
    string? Secret,
    string? RepoPath = null);

public sealed class SaveOpsHostIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork,
    IIntegrationSettingsReloader reloader)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveOpsHostIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The ops host address is required.");
        }

        var username = command.Username?.Trim() ?? string.Empty;
        if (username.Length == 0)
        {
            throw new ValidationException("The ops host SSH username is required.");
        }

        if (!Enum.TryParse<ServerCredentialAuthMethod>(command.AuthMethod, ignoreCase: true, out var authMethod))
        {
            throw new ValidationException($"Unknown auth method '{command.AuthMethod}'. Expected Password or SshKey.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        var secretReference = settings.OpsHostSecretReference;
        if (!string.IsNullOrEmpty(command.Secret))
        {
            secretReference = await secretStore
                .StoreAsync("ops-host/secret", command.Secret, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.ConfigureOpsHost(endpoint, command.Port, username, authMethod, secretReference, command.RepoPath);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await reloader.ReloadAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: false,
            Message: "Ops host settings saved and active immediately.");
    }
}
