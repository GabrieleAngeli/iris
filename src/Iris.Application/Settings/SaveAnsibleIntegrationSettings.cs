using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record SaveAnsibleIntegrationSettingsCommand(
    string Endpoint,
    string Playbook,
    string? Inventory);

public sealed class SaveAnsibleIntegrationSettingsHandler(
    IIntegrationSettingsRepository settingsRepository,
    IUnitOfWork unitOfWork,
    IIntegrationSettingsReloader reloader)
{
    public async Task<IntegrationSettingsSavedResponse> HandleAsync(
        SaveAnsibleIntegrationSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var endpoint = command.Endpoint?.Trim() ?? string.Empty;
        var playbook = command.Playbook?.Trim() ?? string.Empty;
        if (endpoint.Length == 0)
        {
            throw new ValidationException("The Ansible endpoint is required.");
        }

        if (playbook.Length == 0)
        {
            throw new ValidationException("The Ansible playbook is required.");
        }

        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        settings.ConfigureAnsible(endpoint, playbook, command.Inventory);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await reloader.ReloadAsync(cancellationToken).ConfigureAwait(false);

        return new IntegrationSettingsSavedResponse(
            RestartRequired: false,
            Message: "Ansible settings saved and active immediately.");
    }
}
