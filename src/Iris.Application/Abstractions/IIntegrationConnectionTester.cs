using Iris.Application.Settings;

namespace Iris.Application.Abstractions;

/// <summary>
/// Probes a candidate set of integration settings — the values an operator just typed into a
/// "Configure X" dialog, not yet saved — the same way the corresponding <see cref="IIntegrationConnector"/>
/// probes the live, persisted configuration. Backs the mandatory "Test connection" step every
/// Configure dialog requires before Save is enabled. A blank secret field falls back to whatever is
/// already stored, exactly like the corresponding <c>Save*IntegrationSettingsHandler</c>.
/// </summary>
public interface IIntegrationConnectionTester
{
    Task<IntegrationConnectorStatus> TestOpenBaoAsync(SaveOpenBaoIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default);

    Task<IntegrationConnectorStatus> TestAwxAsync(SaveAwxIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default);

    Task<IntegrationConnectorStatus> TestAzureDevOpsAsync(SaveAzureDevOpsIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default);

    Task<IntegrationConnectorStatus> TestNexusAsync(SaveNexusIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default);

    Task<IntegrationConnectorStatus> TestOpsHostAsync(SaveOpsHostIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default);
}
