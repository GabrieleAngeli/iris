using Iris.Domain.Infrastructure;
using Iris.Domain.Settings;
using Iris.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Builds each connector's options from the persisted <see cref="IntegrationSettings"/> row
/// (falling back to <c>IConfiguration</c> when nothing's been saved yet) — the exact same logic
/// used once at process startup (<c>DependencyInjection.RegisterIntegrations</c>) and again,
/// on-demand, by <see cref="IntegrationSettingsReloader"/> after a save, so a changed setting
/// takes effect immediately instead of requiring an <c>Iris.Api</c> restart.
/// </summary>
internal static class IntegrationOptionsFactory
{
    public static OpenBaoOptions BuildOpenBao(IntegrationSettings? persisted, IConfiguration integrations) => new()
    {
        Endpoint = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted.OpenBaoEndpoint
            : integrations["OpenBao:Endpoint"],
        // OpenBao's own token is never resolved from a persisted reference here — doing so would
        // require a working OpenBao connection authenticated with that very token (circular). Only
        // a token given directly via IConfiguration bootstraps OpenBao itself; a token saved
        // through the wizard/UI needs re-entering once after promotion (documented limitation).
        Token = integrations["OpenBao:Token"],
        MountPath = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted!.OpenBaoMountPath
            : integrations["OpenBao:MountPath"] ?? "secret",
        UseKvV2 = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted!.OpenBaoUseKvV2
            : !bool.TryParse(integrations["OpenBao:UseKvV2"], out var useKvV2) || useKvV2,
    };

    public static AnsibleOptions BuildAnsible(IntegrationSettings? persisted, IConfiguration integrations) => new()
    {
        Endpoint = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted.AnsibleEndpoint
            : integrations["Ansible:Endpoint"],
        Playbook = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted!.AnsiblePlaybook
            : integrations["Ansible:Playbook"] ?? "iris-deploy-application.yml",
        Inventory = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted!.AnsibleInventory
            : integrations["Ansible:Inventory"],
    };

    public static AwxOptions BuildAwx(IntegrationSettings? persisted, IConfiguration integrations)
    {
        var awxJobTemplateId = !string.IsNullOrWhiteSpace(persisted?.AwxEndpoint)
            ? persisted!.AwxJobTemplateId
            : int.TryParse(integrations["AWX:JobTemplateId"], out var jobTemplateId) ? jobTemplateId : null;

        var awxFactsJobTemplateId = !string.IsNullOrWhiteSpace(persisted?.AwxEndpoint)
            ? persisted!.AwxFactsJobTemplateId
            : int.TryParse(integrations["AWX:FactsJobTemplateId"], out var factsJobTemplateId) ? factsJobTemplateId : null;

        // AWX secrets are NOT resolved eagerly here: when they were saved through the UI they may
        // sit in the fallback vault, which isn't readable until an admin unlocks it. So pass the
        // persisted reference through and let AwxClient resolve it lazily from the active
        // ISecretStore on first use (config/env values are still used directly).
        var awxOAuthClientId = !string.IsNullOrWhiteSpace(persisted?.AwxOAuthClientId)
            ? persisted!.AwxOAuthClientId
            : integrations["AWX:OAuthClientId"];

        return new AwxOptions
        {
            Endpoint = !string.IsNullOrWhiteSpace(persisted?.AwxEndpoint) ? persisted.AwxEndpoint : integrations["AWX:Endpoint"],
            Token = string.IsNullOrWhiteSpace(persisted?.AwxTokenSecretReference) ? integrations["AWX:Token"] : null,
            TokenSecretReference = persisted?.AwxTokenSecretReference,
            JobTemplateId = awxJobTemplateId,
            FactsJobTemplateId = awxFactsJobTemplateId,
            OAuthClientId = awxOAuthClientId,
            OAuthClientSecret = string.IsNullOrWhiteSpace(persisted?.AwxOAuthClientSecretReference) ? integrations["AWX:OAuthClientSecret"] : null,
            OAuthClientSecretReference = persisted?.AwxOAuthClientSecretReference,
            RefreshToken = string.IsNullOrWhiteSpace(persisted?.AwxRefreshTokenSecretReference) ? integrations["AWX:RefreshToken"] : null,
            RefreshTokenSecretReference = persisted?.AwxRefreshTokenSecretReference,
        };
    }

    public static AzureDevOpsOptions BuildAzureDevOps(IntegrationSettings? persisted, IConfiguration integrations, OpenBaoOptions openBao)
    {
        var azureDevOpsToken = !string.IsNullOrWhiteSpace(persisted?.AzureDevOpsTokenSecretReference)
            ? ResolvePersistedToken(persisted!.AzureDevOpsTokenSecretReference, openBao)
            : integrations["AzureDevOps:Token"];

        return new AzureDevOpsOptions
        {
            Endpoint = !string.IsNullOrWhiteSpace(persisted?.AzureDevOpsEndpoint) ? persisted.AzureDevOpsEndpoint : integrations["AzureDevOps:Endpoint"],
            Token = azureDevOpsToken,
            Project = persisted?.AzureDevOpsProject ?? integrations["AzureDevOps:Project"],
            Repository = persisted?.AzureDevOpsRepository ?? integrations["AzureDevOps:Repository"],
            Branch = persisted?.AzureDevOpsBranch ?? integrations["AzureDevOps:Branch"] ?? "master",
            ManifestPath = persisted?.AzureDevOpsManifestPath ?? integrations["AzureDevOps:ManifestPath"]
                ?? "automation/manifests/awx_context_blueprints.yml",
        };
    }

    public static OpsHostOptions BuildOpsHost(IntegrationSettings? persisted, IConfiguration integrations) => new()
    {
        Endpoint = !string.IsNullOrWhiteSpace(persisted?.OpsHostEndpoint) ? persisted.OpsHostEndpoint : integrations["OpsHost:Endpoint"],
        Port = persisted?.OpsHostPort ?? (int.TryParse(integrations["OpsHost:Port"], out var opsHostPort) ? opsHostPort : 22),
        Username = !string.IsNullOrWhiteSpace(persisted?.OpsHostUsername) ? persisted.OpsHostUsername : integrations["OpsHost:Username"],
        AuthMethod = persisted?.OpsHostAuthMethod ?? ServerCredentialAuthMethod.SshKey,
        Secret = string.IsNullOrWhiteSpace(persisted?.OpsHostSecretReference) ? integrations["OpsHost:Secret"] : null,
        SecretReference = persisted?.OpsHostSecretReference,
        RepoPath = !string.IsNullOrWhiteSpace(persisted?.OpsAwxRepoPath)
            ? persisted.OpsAwxRepoPath
            : integrations["OpsHost:RepoPath"] ?? "/home/ops/Refactoring_ops_flow/awx",
    };

    public static NexusOptions BuildNexus(IntegrationSettings? persisted, IConfiguration integrations, OpenBaoOptions openBao)
    {
        var nexusToken = !string.IsNullOrWhiteSpace(persisted?.NexusTokenSecretReference)
            ? ResolvePersistedToken(persisted!.NexusTokenSecretReference, openBao)
            : integrations["Nexus:Token"];

        return new NexusOptions
        {
            Endpoint = !string.IsNullOrWhiteSpace(persisted?.NexusEndpoint) ? persisted.NexusEndpoint : integrations["Nexus:Endpoint"],
            Token = nexusToken,
        };
    }

    /// <summary>
    /// Resolves a secret reference saved by a previous <c>PUT /system/integrations/*</c> call back
    /// into its raw value, using <paramref name="resolverOpenBao"/> as the (possibly not yet
    /// usable) OpenBao connection to resolve it through. Returns null — logging why, never
    /// throwing — if the reference can't be resolved: a <c>mock-openbao:</c> reference never
    /// survives a restart (it was only ever in that prior process's memory), and an
    /// <c>openbao://</c> reference can't be resolved until <paramref name="resolverOpenBao"/>
    /// itself already has a working endpoint+token.
    /// </summary>
    public static string? ResolvePersistedToken(string? reference, OpenBaoOptions resolverOpenBao)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        if (!resolverOpenBao.IsSecretStoreConfigured)
        {
            Console.Error.WriteLine(
                $"[Iris.Infrastructure] Cannot resolve secret reference '{reference}': " +
                "the OpenBao connection needed to resolve it isn't itself configured yet.");
            return null;
        }

        try
        {
            using var resolver = new OpenBaoSecretStore(resolverOpenBao);
            return resolver.RetrieveAsync(reference).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Iris.Infrastructure] Could not resolve secret reference '{reference}': {ex.Message}");
            return null;
        }
    }
}
