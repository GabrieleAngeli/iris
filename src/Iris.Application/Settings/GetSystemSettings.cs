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

        // A group only "needs a restart" if it was actually persisted (someone called the
        // matching PUT/provision endpoint) AND that persisted value differs from what's active.
        // A group that was never persisted is still legitimately served from
        // appsettings/env config — comparing its null persisted value against a non-null
        // config-provided active value (the real-world case: dev appsettings ships default
        // OpenBao/AWX/Ansible endpoints) would flag RestartRequired forever, for a "change"
        // that never happened. Caught via manual end-to-end verification against a real
        // instance on 2026-09-08 — the original bidirectional-null-safe comparison always
        // returned true in that dev environment; there is a regression test for exactly this.
        var persisted = await integrationSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var restartRequired =
            HasPendingChange(persisted?.OpenBaoEndpoint, activeIntegrations.OpenBaoEndpoint) ||
            HasPendingChange(persisted?.AwxEndpoint, activeIntegrations.AwxEndpoint) ||
            HasPendingChange(persisted?.AnsibleEndpoint, activeIntegrations.AnsibleEndpoint);

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

    /// <summary>True only when this group was actually persisted (<paramref name="persistedEndpoint"/>
    /// non-null) and differs from what's active — never for a group that's still purely
    /// config-driven, no matter what value the active config happens to carry.</summary>
    private static bool HasPendingChange(string? persistedEndpoint, string? activeEndpoint) =>
        persistedEndpoint is not null && !string.Equals(persistedEndpoint, activeEndpoint, StringComparison.Ordinal);

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
