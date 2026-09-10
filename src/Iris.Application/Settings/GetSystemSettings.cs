using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Contracts.Settings;
using Iris.Domain.Settings;

namespace Iris.Application.Settings;

public sealed record GetSystemSettingsQuery(bool CanManageSystem);

public sealed class GetSystemSettingsHandler(
    IMailProviderSettingsRepository mailSettings,
    IIntegrationSettingsRepository integrationSettings,
    ActiveIntegrationSnapshot activeIntegrations,
    IEnumerable<IIntegrationConnector> connectors,
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IFallbackSecretVault fallbackSecretVault,
    IIntegrationHealthMonitor healthMonitor)
{
    public async Task<SystemSettingsResponse> HandleAsync(
        GetSystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetched up front (not just for the RestartRequired flag below): a row saved through
        // one of the PUT endpoints only ever updates this table, never the live connector's own
        // options (those are locked in once, at process start — see ActiveIntegrationSnapshot's
        // remarks). Without surfacing the persisted value here too, a save looked like it did
        // nothing — same status, same (stale) endpoint — until the next restart. Reported by the
        // user as "connesso a OpenBao esistente, nessun problema, ma non salva il token o non
        // sembra comunicare col servizio" on 2026-09-08: the save was real, it just wasn't visible.
        var persisted = await integrationSettings.GetAsync(cancellationToken).ConfigureAwait(false);

        var integrations = new List<IntegrationLinkResponse>();
        foreach (var connector in connectors.OrderBy(connector => connector.Name, StringComparer.OrdinalIgnoreCase))
        {
            var status = await connector.GetStatusAsync(probe: false, cancellationToken).ConfigureAwait(false);
            var link = new IntegrationLinkResponse(status.Key, status.Name, status.Status, status.Endpoint, status.Message);
            link = ApplyOverrides(link, persisted, activeIntegrations, healthMonitor);
            if (link.Key == "awx" && persisted is not null)
            {
                // Non-secret persisted config for the Configure dialog to pre-fill.
                link = link with { AwxJobTemplateId = persisted.AwxJobTemplateId, AwxOAuthClientId = persisted.AwxOAuthClientId };
            }

            integrations.Add(link);
        }

        // OpenBao/Ansible/AWX/Azure DevOps/Nexus all have a real IIntegrationConnector
        // registered (see RegisterIntegrations) and are already covered by the loop above —
        // nothing left needing a fallback placeholder here.

        // A group only "needs a restart" if it was actually persisted (someone called the
        // matching PUT/provision endpoint) AND that persisted value differs from what's active.
        // A group that was never persisted is still legitimately served from
        // appsettings/env config — comparing its null persisted value against a non-null
        // config-provided active value (the real-world case: dev appsettings ships default
        // OpenBao/AWX/Ansible endpoints) would flag RestartRequired forever, for a "change"
        // that never happened. Caught via manual end-to-end verification against a real
        // instance on 2026-09-08 — the original bidirectional-null-safe comparison always
        // returned true in that dev environment; there is a regression test for exactly this.
        var restartRequired =
            HasPendingChange(persisted?.OpenBaoEndpoint, activeIntegrations.OpenBaoEndpoint) ||
            HasPendingChange(persisted?.AwxEndpoint, activeIntegrations.AwxEndpoint) ||
            HasPendingChange(persisted?.AnsibleEndpoint, activeIntegrations.AnsibleEndpoint) ||
            HasPendingChange(persisted?.AzureDevOpsEndpoint, activeIntegrations.AzureDevOpsEndpoint) ||
            HasPendingChange(persisted?.NexusEndpoint, activeIntegrations.NexusEndpoint);

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

        // Only surfaced to platform.admin (same gate as SMTP above), and only bare counts — see
        // FallbackSecretVaultStatus's remarks on why the specific secrets pending are never named.
        // Resolved via EnsureProvisionedAsync (by ExternalId), not ICurrentUser.UserId directly —
        // that claim-based property was found unreliable for a dev-header+password authenticated
        // request in FallbackSecretVaultApiTests's end-to-end test; EnsureProvisionedAsync is the
        // same robust resolution SetMyPasswordHandler already relies on (see ManageMyPassword.cs).
        var currentUserId = currentUser.IsAuthenticated
            ? (await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false)).Id
            : (Guid?)null;
        var vaultStatus = await fallbackSecretVault
            .GetStatusAsync(currentUserId, cancellationToken)
            .ConfigureAwait(false);
        var fallbackSecrets = vaultStatus.HasPendingWork
            ? new FallbackSecretVaultStatusResponse(true, vaultStatus.PendingPersistCount, vaultStatus.RestorableCount)
            : null;

        return new SystemSettingsResponse(true, response, integrations, restartRequired, fallbackSecrets);
    }

    /// <summary>True only when this group was actually persisted (<paramref name="persistedEndpoint"/>
    /// non-null) and differs from what's active — never for a group that's still purely
    /// config-driven, no matter what value the active config happens to carry.</summary>
    private static bool HasPendingChange(string? persistedEndpoint, string? activeEndpoint) =>
        persistedEndpoint is not null && !string.Equals(persistedEndpoint, activeEndpoint, StringComparison.Ordinal);

    /// <summary>For openbao/awx/ansible only, in priority order: (1) a save waiting on a
    /// restart wins — showing a stale background-checked "Unreachable" for the OLD, still-active
    /// config while a NEW one is queued would be confusing, the "Pending restart" message already
    /// says the important thing; (2) otherwise, overlay the last real (<c>probe: true</c>)
    /// reachability result from <see cref="IIntegrationHealthMonitor"/> — requested by the user
    /// (2026-09-08: "un servizio che controlla i servizi connessi se sono raggiungibili") so
    /// System settings/Dashboard reflect actual connectivity, not just "an endpoint is set",
    /// without a live probe on every page load.</summary>
    private static IntegrationLinkResponse ApplyOverrides(
        IntegrationLinkResponse link,
        IntegrationSettings? persisted,
        ActiveIntegrationSnapshot active,
        IIntegrationHealthMonitor healthMonitor)
    {
        var (persistedEndpoint, activeEndpoint) = link.Key switch
        {
            "openbao" => (persisted?.OpenBaoEndpoint, active.OpenBaoEndpoint),
            "awx" => (persisted?.AwxEndpoint, active.AwxEndpoint),
            "ansible" => (persisted?.AnsibleEndpoint, active.AnsibleEndpoint),
            "azure-devops" => (persisted?.AzureDevOpsEndpoint, active.AzureDevOpsEndpoint),
            "nexus" => (persisted?.NexusEndpoint, active.NexusEndpoint),
            _ => (null, null),
        };

        if (HasPendingChange(persistedEndpoint, activeEndpoint))
        {
            return link with
            {
                Status = "Pending restart",
                Endpoint = persistedEndpoint,
                Message = "Saved. Restart Iris.Api to connect using this endpoint.",
            };
        }

        var snapshot = healthMonitor.GetSnapshot(link.Key);
        return snapshot is null
            ? link
            : link with { Status = snapshot.Status, Message = snapshot.Message, CheckedAtUtc = snapshot.CheckedAtUtc };
    }

}
