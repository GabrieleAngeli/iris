using Iris.Application.Abstractions;
using Iris.Application.Settings;
using Iris.Domain.Infrastructure;
using Iris.Infrastructure.Remote;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Implements <see cref="IIntegrationConnectionTester"/> by building a throwaway <c>*Options</c>
/// instance from the candidate values a "Configure X" dialog just sent (never persisted) and
/// running it through the exact same connector class — and therefore the exact same
/// <c>GetStatusAsync(probe: true)</c> logic — as the live, saved configuration. A blank secret
/// field resolves to whatever is already stored, exactly like the corresponding
/// <c>Save*IntegrationSettingsHandler</c>; unlike <c>IntegrationOptionsFactory</c> (used at
/// process-startup/reload time, before the generic <see cref="ISecretStore"/> can safely be
/// trusted), this runs during a live authenticated request, so it can just call
/// <see cref="ISecretStore.RetrieveAsync"/> directly.
/// </summary>
internal sealed class IntegrationConnectionTester(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    ISecretStorePromotion secretStorePromotion) : IIntegrationConnectionTester
{
    public async Task<IntegrationConnectorStatus> TestOpenBaoAsync(
        SaveOpenBaoIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var token = await ResolveSecretAsync(candidate.Token, persisted.OpenBaoTokenSecretReference, cancellationToken).ConfigureAwait(false);

        var options = new OpenBaoOptions
        {
            Endpoint = candidate.Endpoint?.Trim(),
            Token = token,
            MountPath = string.IsNullOrWhiteSpace(candidate.MountPath) ? "secret" : candidate.MountPath.Trim(),
            UseKvV2 = candidate.UseKvV2,
        };

        using var connector = new OpenBaoConnector(options, secretStorePromotion);
        return await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IntegrationConnectorStatus> TestAwxAsync(
        SaveAwxIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        var options = new AwxOptions
        {
            Endpoint = candidate.Endpoint?.Trim(),
            Token = string.IsNullOrEmpty(candidate.Token) ? null : candidate.Token,
            TokenSecretReference = string.IsNullOrEmpty(candidate.Token) ? persisted.AwxTokenSecretReference : null,
            JobTemplateId = candidate.JobTemplateId ?? persisted.AwxJobTemplateId,
            FactsJobTemplateId = candidate.FactsJobTemplateId ?? persisted.AwxFactsJobTemplateId,
            OAuthClientId = string.IsNullOrWhiteSpace(candidate.OAuthClientId) ? persisted.AwxOAuthClientId : candidate.OAuthClientId.Trim(),
            OAuthClientSecret = string.IsNullOrEmpty(candidate.OAuthClientSecret) ? null : candidate.OAuthClientSecret,
            OAuthClientSecretReference = string.IsNullOrEmpty(candidate.OAuthClientSecret) ? persisted.AwxOAuthClientSecretReference : null,
            RefreshToken = string.IsNullOrEmpty(candidate.RefreshToken) ? null : candidate.RefreshToken,
            RefreshTokenSecretReference = string.IsNullOrEmpty(candidate.RefreshToken) ? persisted.AwxRefreshTokenSecretReference : null,
        };

        using var connector = new AwxClient(options, secretStore);
        return await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IntegrationConnectorStatus> TestAzureDevOpsAsync(
        SaveAzureDevOpsIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var token = await ResolveSecretAsync(candidate.Token, persisted.AzureDevOpsTokenSecretReference, cancellationToken).ConfigureAwait(false);

        var project = string.IsNullOrWhiteSpace(candidate.Project) ? persisted.AzureDevOpsProject : candidate.Project.Trim();
        var repository = string.IsNullOrWhiteSpace(candidate.Repository) ? persisted.AzureDevOpsRepository : candidate.Repository.Trim();

        var options = new AzureDevOpsOptions
        {
            Endpoint = candidate.Endpoint?.Trim(),
            Token = token,
            Project = project,
            Repository = repository,
        };
        var branch = string.IsNullOrWhiteSpace(candidate.Branch) ? persisted.AzureDevOpsBranch : candidate.Branch.Trim();
        if (!string.IsNullOrWhiteSpace(branch)) options.Branch = branch;
        var manifestPath = string.IsNullOrWhiteSpace(candidate.ManifestPath) ? persisted.AzureDevOpsManifestPath : candidate.ManifestPath.Trim();
        if (!string.IsNullOrWhiteSpace(manifestPath)) options.ManifestPath = manifestPath;

        using var connector = new AzureDevOpsConnector(options);
        var orgStatus = await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
        if (orgStatus.Status != "Reachable")
        {
            return orgStatus;
        }

        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(repository))
        {
            // Org-level credentials work; no AWX blueprint repo pointer given yet — that's fine,
            // Project/Repository are only required for the AWX-blueprint drift feature, not for
            // the org-level functions (e.g. the per-application scaffold-PR override).
            return orgStatus;
        }

        try
        {
            var manifest = await connector
                .GetFileContentAsync(project!, repository!, options.Branch, options.ManifestPath, cancellationToken)
                .ConfigureAwait(false);
            if (manifest is null)
            {
                return orgStatus with
                {
                    Status = "Unreachable",
                    Message = $"Organization reachable, but no file was found at {project}/{repository}@{options.Branch}:{options.ManifestPath}.",
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return orgStatus with { Status = "Unreachable", Message = ex.Message };
        }

        return orgStatus with { Message = "Organization reachable; blueprint manifest found." };
    }

    public async Task<IntegrationConnectorStatus> TestNexusAsync(
        SaveNexusIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var token = await ResolveSecretAsync(candidate.Token, persisted.NexusTokenSecretReference, cancellationToken).ConfigureAwait(false);

        var options = new NexusOptions
        {
            Endpoint = candidate.Endpoint?.Trim(),
            Token = token,
        };

        using var connector = new NexusConnector(options);
        return await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IntegrationConnectorStatus> TestOpsHostAsync(
        SaveOpsHostIntegrationSettingsCommand candidate, CancellationToken cancellationToken = default)
    {
        var persisted = await settingsRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        if (!Enum.TryParse<ServerCredentialAuthMethod>(candidate.AuthMethod, ignoreCase: true, out var authMethod))
        {
            return new IntegrationConnectorStatus(
                "ops-host", "Ops host", "Not configured", candidate.Endpoint, $"Unknown auth method '{candidate.AuthMethod}'.");
        }

        var options = new OpsHostOptions
        {
            Endpoint = candidate.Endpoint?.Trim(),
            Port = candidate.Port,
            Username = candidate.Username?.Trim(),
            AuthMethod = authMethod,
            Secret = string.IsNullOrEmpty(candidate.Secret) ? null : candidate.Secret,
            SecretReference = string.IsNullOrEmpty(candidate.Secret) ? persisted.OpsHostSecretReference : null,
            RepoPath = string.IsNullOrWhiteSpace(candidate.RepoPath) ? persisted.OpsAwxRepoPath : candidate.RepoPath.Trim(),
        };

        var connector = new OpsHostConnector(options, secretStore, new SshCommandRunner());
        return await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ResolveSecretAsync(string? typed, string? persistedReference, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(typed))
        {
            return typed;
        }

        if (string.IsNullOrEmpty(persistedReference))
        {
            return null;
        }

        try
        {
            return await secretStore.RetrieveAsync(persistedReference, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Let the connector's own probe report why it's unreachable rather than throwing here.
            return null;
        }
    }
}
