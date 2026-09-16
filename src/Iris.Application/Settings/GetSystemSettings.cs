using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Contracts.Settings;
using Iris.Domain.Settings;

namespace Iris.Application.Settings;

public sealed record GetSystemSettingsQuery(bool CanManageSystem);

public sealed class GetSystemSettingsHandler(
    IMailProviderSettingsRepository mailSettings,
    IIntegrationSettingsRepository integrationSettings,
    IEnumerable<IIntegrationConnector> connectors,
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IFallbackSecretVault fallbackSecretVault,
    IIntegrationHealthMonitor healthMonitor,
    ISecretStorePromotion secretStorePromotion)
{
    public async Task<SystemSettingsResponse> HandleAsync(
        GetSystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A save through PUT /system/integrations/* now reconfigures the live connector
        // immediately (IIntegrationSettingsReloader) — this read is only for pre-filling the
        // Configure dialogs' non-secret fields below, not for detecting a stale/pending value.
        var persisted = await integrationSettings.GetAsync(cancellationToken).ConfigureAwait(false);

        var integrations = new List<IntegrationLinkResponse>();
        foreach (var connector in connectors.OrderBy(connector => connector.Name, StringComparer.OrdinalIgnoreCase))
        {
            var status = await connector.GetStatusAsync(probe: false, cancellationToken).ConfigureAwait(false);
            var link = new IntegrationLinkResponse(status.Key, status.Name, status.Status, status.Endpoint, status.Message);
            link = ApplyHealthSnapshot(link, healthMonitor);
            if (link.Key == "openbao")
            {
                link = link with { IsSecretStoreActive = secretStorePromotion.IsOpenBaoActive };
            }

            if (link.Key == "awx" && persisted is not null)
            {
                // Non-secret persisted config for the Configure dialog to pre-fill.
                link = link with
                {
                    AwxJobTemplateId = persisted.AwxJobTemplateId,
                    AwxOAuthClientId = persisted.AwxOAuthClientId,
                    AwxFactsJobTemplateId = persisted.AwxFactsJobTemplateId,
                };
            }

            if (link.Key == "azure-devops" && persisted is not null)
            {
                link = link with
                {
                    AzureDevOpsProject = persisted.AzureDevOpsProject,
                    AzureDevOpsRepository = persisted.AzureDevOpsRepository,
                    AzureDevOpsBranch = persisted.AzureDevOpsBranch,
                    AzureDevOpsManifestPath = persisted.AzureDevOpsManifestPath,
                };
            }

            if (link.Key == "ops-host" && persisted is not null)
            {
                link = link with
                {
                    OpsHostPort = persisted.OpsHostPort,
                    OpsHostUsername = persisted.OpsHostUsername,
                    OpsHostAuthMethod = persisted.OpsHostAuthMethod.ToString(),
                    OpsAwxRepoPath = persisted.OpsAwxRepoPath,
                };
            }

            integrations.Add(link);
        }

        // OpenBao/Ansible/AWX/Azure DevOps/Nexus all have a real IIntegrationConnector
        // registered (see RegisterIntegrations) and are already covered by the loop above —
        // nothing left needing a fallback placeholder here.

        if (!query.CanManageSystem)
        {
            return new SystemSettingsResponse(false, null, integrations, RestartRequired: false);
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

        return new SystemSettingsResponse(true, response, integrations, RestartRequired: false, fallbackSecrets);
    }

    /// <summary>Overlays the last real (<c>probe: true</c>) reachability result from
    /// <see cref="IIntegrationHealthMonitor"/> — requested by the user (2026-09-08: "un servizio
    /// che controlla i servizi connessi se sono raggiungibili") so System settings/Dashboard
    /// reflect actual connectivity, not just "an endpoint is set", without a live probe on every
    /// page load.</summary>
    private static IntegrationLinkResponse ApplyHealthSnapshot(IntegrationLinkResponse link, IIntegrationHealthMonitor healthMonitor)
    {
        var snapshot = healthMonitor.GetSnapshot(link.Key);
        return snapshot is null
            ? link
            : link with { Status = snapshot.Status, Message = snapshot.Message, CheckedAtUtc = snapshot.CheckedAtUtc };
    }
}
