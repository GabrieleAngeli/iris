namespace Iris.Contracts.Settings;

/// <summary>Body of <c>PUT /system/integrations/openbao</c>. <c>Token</c> left empty/null
/// keeps whatever token reference is already stored — it does not clear it.</summary>
public sealed record SaveOpenBaoIntegrationSettingsRequest(
    string Endpoint,
    string? Token,
    string MountPath,
    bool UseKvV2);

/// <summary>Body of <c>PUT /system/integrations/awx</c>. Same empty-token rule as OpenBao, for
/// each of <c>Token</c>/<c>OAuthClientSecret</c>/<c>RefreshToken</c>. Supplying
/// <c>OAuthClientId</c>+<c>OAuthClientSecret</c>+<c>RefreshToken</c> turns on OAuth2
/// auto-renewal of the access token (AWX tokens expire).</summary>
public sealed record SaveAwxIntegrationSettingsRequest(
    string Endpoint,
    string? Token,
    int? JobTemplateId,
    string? OAuthClientId = null,
    string? OAuthClientSecret = null,
    string? RefreshToken = null);

/// <summary>Body of <c>PUT /system/integrations/ansible</c>. No secret — this describes a
/// playbook/inventory target, not a credentialed API.</summary>
public sealed record SaveAnsibleIntegrationSettingsRequest(
    string Endpoint,
    string Playbook,
    string? Inventory);

/// <summary>Body of <c>PUT /system/integrations/azure-devops</c>. <c>Endpoint</c> is the
/// organization URL (e.g. <c>https://dev.azure.com/your-org</c>); same empty-token rule as
/// OpenBao/AWX.</summary>
public sealed record SaveAzureDevOpsIntegrationSettingsRequest(
    string Endpoint,
    string? Token);

/// <summary>Body of <c>PUT /system/integrations/nexus</c>. Same empty-token rule as OpenBao/AWX.</summary>
public sealed record SaveNexusIntegrationSettingsRequest(
    string Endpoint,
    string? Token);

/// <summary><c>RestartRequired</c> is always <c>true</c> here: a save always changes the
/// persisted row, and <c>RegisterIntegrations</c> only reads it once, at process startup
/// (see <c>Iris.Infrastructure/DependencyInjection.cs</c>) — so the change only takes
/// effect in the running process after an Iris.Api restart.</summary>
public sealed record IntegrationSettingsSavedResponse(bool RestartRequired, string Message);

/// <summary>
/// Result of <c>POST /system/integrations/openbao/provision</c> — a convenience/dev-mode
/// OpenBao container started on the same host Iris.Api runs on. Not for production use (no
/// persistent storage backend, no unseal/HA story); an operator who needs that should
/// configure an existing hardened OpenBao instance via <c>PUT /system/integrations/openbao</c>
/// instead of this endpoint.
/// </summary>
public sealed record ProvisionOpenBaoResponse(string Endpoint, bool RestartRequired, string Message);
