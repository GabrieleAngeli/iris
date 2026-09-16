using Iris.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Re-syncs the six connector options singletons (registered as already-constructed instances in
/// <c>DependencyInjection.RegisterIntegrations</c> — every connector holds a reference to the
/// exact same object) with whatever is now persisted, by mutating their properties in place. Since
/// every consumer (<c>AwxClient</c>, <c>NexusConnector</c>, ...) shares that same instance, this
/// takes effect immediately, with no changes needed to any connector class.
///
/// Deliberately does not touch <see cref="AwxOptions"/>'s OAuth access/refresh token: that's
/// rotated at runtime by <c>AwxClient</c> itself (<c>EnsureCredentialsAsync</c>/
/// <c>SendWithAuthRetryAsync</c>) into its own private state, not this bootstrap-only field —
/// re-syncing it here would fight that rotation.
/// </summary>
internal sealed class IntegrationSettingsReloader(
    IIntegrationSettingsRepository settingsRepository,
    IConfiguration configuration,
    OpenBaoOptions openBao,
    AnsibleOptions ansible,
    AwxOptions awx,
    AzureDevOpsOptions azureDevOps,
    OpsHostOptions opsHost,
    NexusOptions nexus) : IIntegrationSettingsReloader
{
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var integrations = configuration.GetSection("Iris:Integrations");

        var freshOpenBao = IntegrationOptionsFactory.BuildOpenBao(persisted, integrations);
        openBao.Endpoint = freshOpenBao.Endpoint;
        openBao.MountPath = freshOpenBao.MountPath;
        openBao.UseKvV2 = freshOpenBao.UseKvV2;
        // Token intentionally left alone — see BuildOpenBao's remarks (circular resolution).

        var freshAnsible = IntegrationOptionsFactory.BuildAnsible(persisted, integrations);
        ansible.Endpoint = freshAnsible.Endpoint;
        ansible.Playbook = freshAnsible.Playbook;
        ansible.Inventory = freshAnsible.Inventory;

        var freshAwx = IntegrationOptionsFactory.BuildAwx(persisted, integrations);
        awx.Endpoint = freshAwx.Endpoint;
        awx.Token = freshAwx.Token;
        awx.TokenSecretReference = freshAwx.TokenSecretReference;
        awx.JobTemplateId = freshAwx.JobTemplateId;
        awx.FactsJobTemplateId = freshAwx.FactsJobTemplateId;
        awx.OAuthClientId = freshAwx.OAuthClientId;
        awx.OAuthClientSecret = freshAwx.OAuthClientSecret;
        awx.OAuthClientSecretReference = freshAwx.OAuthClientSecretReference;
        awx.RefreshToken = freshAwx.RefreshToken;
        awx.RefreshTokenSecretReference = freshAwx.RefreshTokenSecretReference;

        var freshAzureDevOps = IntegrationOptionsFactory.BuildAzureDevOps(persisted, integrations, openBao);
        azureDevOps.Endpoint = freshAzureDevOps.Endpoint;
        azureDevOps.Token = freshAzureDevOps.Token;
        azureDevOps.Project = freshAzureDevOps.Project;
        azureDevOps.Repository = freshAzureDevOps.Repository;
        azureDevOps.Branch = freshAzureDevOps.Branch;
        azureDevOps.ManifestPath = freshAzureDevOps.ManifestPath;

        var freshOpsHost = IntegrationOptionsFactory.BuildOpsHost(persisted, integrations);
        opsHost.Endpoint = freshOpsHost.Endpoint;
        opsHost.Port = freshOpsHost.Port;
        opsHost.Username = freshOpsHost.Username;
        opsHost.AuthMethod = freshOpsHost.AuthMethod;
        opsHost.Secret = freshOpsHost.Secret;
        opsHost.SecretReference = freshOpsHost.SecretReference;
        opsHost.RepoPath = freshOpsHost.RepoPath;

        var freshNexus = IntegrationOptionsFactory.BuildNexus(persisted, integrations, openBao);
        nexus.Endpoint = freshNexus.Endpoint;
        nexus.Token = freshNexus.Token;
    }
}
