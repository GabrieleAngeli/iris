using Iris.Application.Abstractions;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record GetSystemSettingsQuery(
    bool CanManageSystem,
    string? OpenBaoEndpoint,
    string? AnsibleEndpoint,
    string? AwxEndpoint,
    string? AzureDevOpsEndpoint,
    string? NexusEndpoint);

public sealed class GetSystemSettingsHandler(
    IMailProviderSettingsRepository mailSettings,
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

        AddIfMissing(integrations, Link("openbao", "OpenBao", query.OpenBaoEndpoint));
        AddIfMissing(integrations, Link("ansible", "Ansible", query.AnsibleEndpoint));
        AddIfMissing(integrations, Link("awx", "AWX", query.AwxEndpoint));
        AddIfMissing(integrations, Link("azure-devops", "Azure DevOps", query.AzureDevOpsEndpoint));
        AddIfMissing(integrations, Link("nexus", "Nexus Repository", query.NexusEndpoint));

        if (!query.CanManageSystem)
        {
            return new SystemSettingsResponse(false, null, integrations);
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

        return new SystemSettingsResponse(true, response, integrations);
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
