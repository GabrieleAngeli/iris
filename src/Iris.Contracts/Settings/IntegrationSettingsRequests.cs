namespace Iris.Contracts.Settings;

/// <summary>Body of <c>PUT /system/integrations/openbao</c>. <c>Token</c> left empty/null
/// keeps whatever token reference is already stored — it does not clear it.</summary>
public sealed record SaveOpenBaoIntegrationSettingsRequest(
    string Endpoint,
    string? Token,
    string MountPath,
    bool UseKvV2);

/// <summary>Body of <c>PUT /system/integrations/awx</c>. Same empty-token rule as OpenBao.</summary>
public sealed record SaveAwxIntegrationSettingsRequest(
    string Endpoint,
    string? Token,
    int? JobTemplateId);

/// <summary>Body of <c>PUT /system/integrations/ansible</c>. No secret — this describes a
/// playbook/inventory target, not a credentialed API.</summary>
public sealed record SaveAnsibleIntegrationSettingsRequest(
    string Endpoint,
    string Playbook,
    string? Inventory);

/// <summary><c>RestartRequired</c> is always <c>true</c> here: a save always changes the
/// persisted row, and <c>RegisterIntegrations</c> only reads it once, at process startup
/// (see <c>Iris.Infrastructure/DependencyInjection.cs</c>) — so the change only takes
/// effect in the running process after an Iris.Api restart.</summary>
public sealed record IntegrationSettingsSavedResponse(bool RestartRequired, string Message);
