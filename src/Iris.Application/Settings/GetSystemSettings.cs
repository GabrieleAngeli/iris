using Iris.Application.Abstractions;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record GetSystemSettingsQuery(
    bool CanManageSystem,
    string? AzureDevOpsEndpoint,
    string? NexusEndpoint);

public sealed class GetSystemSettingsHandler(
    IMailProviderSettingsRepository mailSettings,
    IIntegrationSettingsRepository integrationSettings,
    ActiveIntegrationSnapshot activeIntegrations,
    IEnumerable<IIntegrationConnector> connectors)
{
    public async Task<SystemSettingsResponse> HandleAsync(
        GetSystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var integrations = new List<IntegrationLinkResponse>();
        foreach (var connector in connectors.OrderBy(connector => connector.Name, StringComparer.OrdinalIgnoreCase))
        {
            var status = await connector.GetStatusAsync(probe: false, cancellationToken).ConfigureAwait(false);
            integrations.Add(new IntegrationLinkResponse(status.Key, status.Name, status.Status, status.Endpoint, status.Message));
        }

        // OpenBao/Ansible/AWX always have a real IIntegrationConnector registered
        // (OpenBaoConnector/AnsibleExecutionPackageBuilder/AwxClient — see RegisterIntegrations),
        // so they're already covered by the loop above. Only integrations with no connector
        // of their own need this fallback.
        AddIfMissing(integrations, Link("azure-devops", "Azure DevOps", query.AzureDevOpsEndpoint));
        AddIfMissing(integrations, Link("nexus", "Nexus Repository", query.NexusEndpoint));

        var persisted = await integrationSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var restartRequired =
            !string.Equals(activeIntegrations.OpenBaoEndpoint, persisted?.OpenBaoEndpoint, StringComparison.Ordinal) ||
            !string.Equals(activeIntegrations.AwxEndpoint, persisted?.AwxEndpoint, StringComparison.Ordinal) ||
            !string.Equals(activeIntegrations.AnsibleEndpoint, persisted?.AnsibleEndpoint, StringComparison.Ordinal);

        if (!query.CanManageSystem)
        {
            return new SystemSettingsResponse(false, null, integrations, restartRequired);
        }

        var mail = await mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var response = mail is null
            ? new MailProviderSettingsResponse(false, null, null, null, null, null, false)
            : new MailProviderSettingsResponse(
                true,
                mail.SmtpHost,
                mail.SmtpPort,
                mail.SmtpUsername,
                mail.FromAddress,
                mail.FromDisplayName,
                mail.EnableSsl);

        return new SystemSettingsResponse(true, response, integrations, restartRequired);
    }

    private static IntegrationLinkResponse Link(string key, string name, string? endpoint) =>
        new(key, name, string.IsNullOrWhiteSpace(endpoint) ? "Not configured" : "Configured", endpoint);

    private static void AddIfMissing(List<IntegrationLinkResponse> integrations, IntegrationLinkResponse link)
    {
        if (integrations.All(item => !string.Equals(item.Key, link.Key, StringComparison.OrdinalIgnoreCase)))
        {
            integrations.Add(link);
        }
    }
}
