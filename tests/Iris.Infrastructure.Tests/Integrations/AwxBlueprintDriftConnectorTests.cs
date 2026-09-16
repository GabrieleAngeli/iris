using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class AwxBlueprintDriftConnectorTests
{
    private const string Manifest = """
        awx_context_blueprints:
          - context_name: cloud_02-trial
            inventory_name: cloud_02-trial
            templates:
              - name: cloud_02-trial-site
                description: site flow
                playbook: automation/orchestration/cloud_02/trial/site.yml
              - name: cloud_02-trial-facts
                description: facts discovery
                playbook: playbooks/validation/discover-host-facts.yml
                use_fact_cache: true
        """;

    private sealed class FakeRepositoryReader(string? content) : IAzureDevOpsRepositoryReader
    {
        public string? Content { get; set; } = content;

        public Task<string?> GetFileContentAsync(
            string project, string repository, string branch, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Content);
    }

    private sealed class FakeAwxClient : IAwxClient
    {
        public Dictionary<string, AwxJobTemplateInfo> Templates { get; } = new(StringComparer.Ordinal);

        public Task<AwxJobLaunchResult> LaunchAsync(AwxJobLaunch launch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AwxJobStatusResult> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetJobOutputAsync(string jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AwxHostFactsResult> GetHostFactsAsync(int jobTemplateId, string hostname, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AwxJobTemplateInfo?> GetJobTemplateAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Templates.GetValueOrDefault(name));
    }

    private static AzureDevOpsOptions ConfiguredAzureDevOps() => new()
    {
        Endpoint = "https://dev.azure.com/algorab-devops",
        Token = "pat",
        Project = "Refactoring_ops_flow",
        Repository = "awx",
        Branch = "master",
        ManifestPath = "automation/manifests/awx_context_blueprints.yml",
    };

    private static AwxOptions ConfiguredAwx() => new()
    {
        Endpoint = "https://awx.example",
        Token = "token",
        JobTemplateId = 7,
    };

    [Fact]
    public async Task Reports_in_sync_when_every_declared_template_matches_awx()
    {
        var awx = new FakeAwxClient();
        awx.Templates["cloud_02-trial-site"] = new AwxJobTemplateInfo(1, "automation/orchestration/cloud_02/trial/site.yml", false);
        awx.Templates["cloud_02-trial-facts"] = new AwxJobTemplateInfo(2, "playbooks/validation/discover-host-facts.yml", true);
        var connector = new AwxBlueprintDriftConnector(new FakeRepositoryReader(Manifest), ConfiguredAzureDevOps(), awx, ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: true);

        Assert.Equal("In sync", status.Status);
    }

    [Fact]
    public async Task Reports_drift_when_a_declared_template_is_missing_in_awx()
    {
        var awx = new FakeAwxClient();
        awx.Templates["cloud_02-trial-site"] = new AwxJobTemplateInfo(1, "automation/orchestration/cloud_02/trial/site.yml", false);
        // cloud_02-trial-facts intentionally missing — not synced yet.
        var connector = new AwxBlueprintDriftConnector(new FakeRepositoryReader(Manifest), ConfiguredAzureDevOps(), awx, ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: true);

        Assert.Equal("Drift detected", status.Status);
        Assert.Contains("cloud_02-trial-facts", status.Message);
    }

    [Fact]
    public async Task Reports_drift_when_use_fact_cache_does_not_match()
    {
        var awx = new FakeAwxClient();
        awx.Templates["cloud_02-trial-site"] = new AwxJobTemplateInfo(1, "automation/orchestration/cloud_02/trial/site.yml", false);
        awx.Templates["cloud_02-trial-facts"] = new AwxJobTemplateInfo(2, "playbooks/validation/discover-host-facts.yml", false); // wrong
        var connector = new AwxBlueprintDriftConnector(new FakeRepositoryReader(Manifest), ConfiguredAzureDevOps(), awx, ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: true);

        Assert.Equal("Drift detected", status.Status);
        Assert.Contains("cloud_02-trial-facts", status.Message);
    }

    [Fact]
    public async Task Reports_not_configured_when_azure_devops_repository_is_missing()
    {
        var connector = new AwxBlueprintDriftConnector(
            new FakeRepositoryReader(Manifest), new AzureDevOpsOptions { Endpoint = "https://dev.azure.com/x", Token = "t" },
            new FakeAwxClient(), ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: true);

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task Reports_unreachable_when_the_manifest_is_not_found()
    {
        var connector = new AwxBlueprintDriftConnector(new FakeRepositoryReader(null), ConfiguredAzureDevOps(), new FakeAwxClient(), ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: true);

        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task Probe_false_does_not_fetch_the_manifest()
    {
        var reader = new FakeRepositoryReader(Manifest);
        var connector = new AwxBlueprintDriftConnector(reader, ConfiguredAzureDevOps(), new FakeAwxClient(), ConfiguredAwx());

        var status = await connector.GetStatusAsync(probe: false);

        Assert.Equal("Configured", status.Status);
    }
}
