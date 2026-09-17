namespace Iris.Contracts.Settings;

public sealed record MailProviderSettingsResponse(
    bool IsConfigured,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? FromAddress,
    string? FromDisplayName,
    bool EnableSsl);

public sealed record IntegrationLinkResponse(
    string Key,
    string Name,
    string Status,
    string? Endpoint,
    string? Message = null,
    DateTimeOffset? CheckedAtUtc = null,
    // Non-secret persisted config, echoed back so the "Configure" dialog can pre-fill and the
    // operator doesn't have to re-type (or accidentally wipe) them. AWX-only for now.
    int? AwxJobTemplateId = null,
    string? AwxOAuthClientId = null,
    int? AwxFactsJobTemplateId = null,
    // Set for "azure-devops" AND "awx-blueprint" — the org URL a Configure dialog should pre-fill
    // (distinct from Endpoint, which for "awx-blueprint" is a human-readable
    // "project/repo@branch:path" summary, not the organization URL).
    string? AzureDevOpsEndpoint = null,
    string? AzureDevOpsProject = null,
    string? AzureDevOpsRepository = null,
    string? AzureDevOpsBranch = null,
    string? AzureDevOpsManifestPath = null,
    int? OpsHostPort = null,
    string? OpsHostUsername = null,
    string? OpsHostAuthMethod = null,
    string? OpsAwxRepoPath = null,
    // "openbao" only: whether OpenBao is genuinely the active secret store right now (not just
    // configured/reachable) — lets the client hide Promote/Provision once there's nothing left
    // to do. Meaningless (always false) for every other connector.
    bool IsSecretStoreActive = false);

/// <summary>Non-null only when there's something to unlock (bare counts, never which secrets —
/// see <c>IFallbackSecretVault</c>'s remarks). Drives the "Unlock secrets" banner in
/// System settings.</summary>
public sealed record FallbackSecretVaultStatusResponse(
    bool HasPendingWork,
    int PendingPersistCount,
    int RestorableCount);

public sealed record SystemSettingsResponse(
    bool CanManageSystem,
    MailProviderSettingsResponse? Mail,
    IReadOnlyList<IntegrationLinkResponse> Integrations,
    bool RestartRequired = false,
    FallbackSecretVaultStatusResponse? FallbackSecrets = null);
