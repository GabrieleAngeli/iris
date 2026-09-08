namespace Iris.Application.Abstractions;

/// <summary>
/// What this running process actually locked in for OpenBao/AWX/Ansible/Azure DevOps/Nexus at
/// startup.
/// <c>RegisterIntegrations</c> (<c>Iris.Infrastructure/DependencyInjection.cs</c>) reads
/// configuration/persisted <c>IntegrationSettings</c> once, at process start — DI
/// singletons built from it don't change after that. This snapshot is registered at the
/// same time so the Application layer can compare "what's active" against "what's now
/// persisted" without depending on Infrastructure's internal options types (which it must
/// not reference — <c>Iris.Application</c> has no project reference to
/// <c>Iris.Infrastructure</c>). Used by <c>GetSystemSettingsHandler</c> to compute
/// <c>SystemSettingsResponse.RestartRequired</c>.
/// </summary>
public sealed record ActiveIntegrationSnapshot(
    string? OpenBaoEndpoint,
    string? AwxEndpoint,
    string? AnsibleEndpoint,
    string? AzureDevOpsEndpoint = null,
    string? NexusEndpoint = null);
