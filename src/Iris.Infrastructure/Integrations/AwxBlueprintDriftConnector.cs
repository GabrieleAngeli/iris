using Iris.Application.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Compares the AWX automation repo's declared blueprint manifest
/// (<c>automation/manifests/awx_context_blueprints.yml</c> in <c>Refactoring_ops_flow/awx</c>,
/// read via <see cref="IAzureDevOpsRepositoryReader"/>) against what's actually configured in AWX
/// (<see cref="IAwxClient.GetJobTemplateAsync"/>) — name/playbook/<c>use_fact_cache</c> per
/// declared template. This is the "has AWX drifted from what the repo says it should be" signal
/// that drives the "Sync now" button (<c>SyncAwxBlueprintHandler</c>); it never changes anything
/// itself, only reports.
/// </summary>
internal sealed class AwxBlueprintDriftConnector(
    IAzureDevOpsRepositoryReader repositoryReader,
    AzureDevOpsOptions azureDevOpsOptions,
    IAwxClient awx,
    AwxOptions awxOptions) : IIntegrationConnector
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string Key => "awx-blueprint";

    public string Name => "AWX blueprint";

    public string? Endpoint => azureDevOpsOptions.Repository is { Length: > 0 } repository
        ? $"{azureDevOpsOptions.Project}/{repository}@{azureDevOpsOptions.Branch}:{azureDevOpsOptions.ManifestPath}"
        : null;

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (!azureDevOpsOptions.CanReadManifest)
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", Endpoint, "Azure DevOps project/repository are required.");
        }

        if (!awxOptions.IsConfigured)
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", Endpoint, "AWX endpoint/token/job template id are required.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint);
        }

        try
        {
            var manifestText = await repositoryReader
                .GetFileContentAsync(
                    azureDevOpsOptions.Project!, azureDevOpsOptions.Repository!, azureDevOpsOptions.Branch, azureDevOpsOptions.ManifestPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (manifestText is null)
            {
                return new IntegrationConnectorStatus(
                    Key, Name, "Unreachable", Endpoint, $"Manifest not found at {azureDevOpsOptions.ManifestPath}.");
            }

            var manifest = Deserializer.Deserialize<BlueprintManifest>(manifestText);
            var declaredTemplates = (manifest.AwxContextBlueprints ?? [])
                .SelectMany(context => context.Templates ?? [])
                .ToList();

            if (declaredTemplates.Count == 0)
            {
                return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, "Manifest declares no templates.");
            }

            var drifted = new List<string>();
            foreach (var declared in declaredTemplates)
            {
                var actual = await awx.GetJobTemplateAsync(declared.Name, cancellationToken).ConfigureAwait(false);
                if (actual is null ||
                    !string.Equals(actual.Playbook, declared.Playbook, StringComparison.Ordinal) ||
                    actual.UseFactCache != declared.UseFactCache)
                {
                    drifted.Add(declared.Name);
                }
            }

            return drifted.Count == 0
                ? new IntegrationConnectorStatus(Key, Name, "In sync", Endpoint, $"{declaredTemplates.Count} template(s) checked.")
                : new IntegrationConnectorStatus(
                    Key, Name, "Drift detected", Endpoint, $"{drifted.Count} template(s) out of sync: {string.Join(", ", drifted)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Covers: ValidationException from an unconfigured/unreachable AwxClient,
            // HttpRequestException from the Azure DevOps read, YamlDotNet parse failures — this
            // probe must never throw, only report "Unreachable" with why.
            return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, ex.Message);
        }
    }

    private sealed class BlueprintManifest
    {
        public List<BlueprintContext>? AwxContextBlueprints { get; set; }
    }

    private sealed class BlueprintContext
    {
        public List<BlueprintTemplate>? Templates { get; set; }
    }

    private sealed class BlueprintTemplate
    {
        public string Name { get; set; } = string.Empty;

        public string? Playbook { get; set; }

        public bool UseFactCache { get; set; }
    }
}
