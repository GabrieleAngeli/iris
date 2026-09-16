using Iris.Application.Abstractions;
using Iris.Application.Applications;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Applications;
using Iris.Domain.Settings;

namespace Iris.Application.Tests.Applications;

public sealed class ProposeAnsibleScaffoldToAwxRepoHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 9, 30, 0, TimeSpan.Zero);

    private static CreateApplicationHandler CreateHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static AddApplicationVersionHandler AddVersionHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static ProposeAnsibleScaffoldToAwxRepoHandler Handler(FakeStore store, FakeAzureDevOpsRepositoryWriter writer) =>
        new(
            new GenerateApplicationAnsibleScaffoldHandler(store.ApplicationRepository, new FakeClock(Now)),
            store.IntegrationSettingsRepository,
            writer,
            new FakeClock(Now));

    private static async Task<(Guid AppId, Guid VersionId)> SeedApplicationVersion(FakeStore store)
    {
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine", null, "Java", "https://git.example/augeg4-engine", "main", null));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "4.0.0", "refs/tags/4.0.0", new RuntimeMetadataRequest("java17", "Linux", 2, 1024, [8080])));
        return (app.Id, version.Id);
    }

    [Fact]
    public async Task Propose_throws_validation_error_when_azure_devops_project_or_repository_is_not_configured()
    {
        var store = new FakeStore();
        var (appId, versionId) = await SeedApplicationVersion(store);
        var writer = new FakeAzureDevOpsRepositoryWriter();

        await Assert.ThrowsAsync<ValidationException>(() => Handler(store, writer).HandleAsync(
            new ProposeAnsibleScaffoldToAwxRepoCommand(appId, versionId)));
    }

    [Fact]
    public async Task Propose_remaps_scaffold_paths_onto_the_awx_repo_conventions_and_uses_a_dated_branch_name()
    {
        var store = new FakeStore();
        var settings = await store.IntegrationSettingsRepository.GetOrCreateAsync();
        settings.ConfigureAzureDevOps("https://dev.azure.com/algorab-devops", null, "Refactoring_ops_flow", "awx", "master", null);
        var (appId, versionId) = await SeedApplicationVersion(store);
        var writer = new FakeAzureDevOpsRepositoryWriter();

        var result = await Handler(store, writer).HandleAsync(new ProposeAnsibleScaffoldToAwxRepoCommand(appId, versionId));

        Assert.Equal(101, result.PullRequestId);
        Assert.Equal("https://example.invalid/pr/101", result.Url);

        var proposal = Assert.Single(writer.Proposals);
        Assert.Equal("Refactoring_ops_flow", proposal.Project);
        Assert.Equal("awx", proposal.Repository);
        Assert.Equal("master", proposal.BaseBranch);
        Assert.Equal("iris/ansible-scaffold/augeg4-engine-4.0.0-20260916093000", proposal.NewBranchName);
        Assert.Contains("augeg4-engine", proposal.CommitMessage);

        Assert.True(proposal.Files.ContainsKey("roles/augeg4-engine/README.md"));
        Assert.False(proposal.Files.ContainsKey("README.md"));
        Assert.True(proposal.Files.ContainsKey("playbooks/applications/augeg4-engine.yml"));
        Assert.False(proposal.Files.ContainsKey("playbooks/augeg4-engine.yml"));
        Assert.Contains(proposal.Files.Keys, path => path.StartsWith("roles/augeg4-engine/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Propose_falls_back_to_master_when_no_branch_is_configured()
    {
        var store = new FakeStore();
        var settings = await store.IntegrationSettingsRepository.GetOrCreateAsync();
        settings.ConfigureAzureDevOps("https://dev.azure.com/algorab-devops", null, "Refactoring_ops_flow", "awx", null, null);
        var (appId, versionId) = await SeedApplicationVersion(store);
        var writer = new FakeAzureDevOpsRepositoryWriter();

        await Handler(store, writer).HandleAsync(new ProposeAnsibleScaffoldToAwxRepoCommand(appId, versionId));

        Assert.Equal("master", Assert.Single(writer.Proposals).BaseBranch);
    }
}

internal sealed class FakeAzureDevOpsRepositoryWriter : IAzureDevOpsRepositoryWriter
{
    public List<AzureDevOpsChangeProposal> Proposals { get; } = [];

    public Task<AzureDevOpsPullRequestResult> ProposeChangeAsync(
        AzureDevOpsChangeProposal proposal, CancellationToken cancellationToken = default)
    {
        Proposals.Add(proposal);
        return Task.FromResult(new AzureDevOpsPullRequestResult(101, "https://example.invalid/pr/101"));
    }
}
