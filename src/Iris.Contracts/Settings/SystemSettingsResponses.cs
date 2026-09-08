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
    DateTimeOffset? CheckedAtUtc = null);

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
