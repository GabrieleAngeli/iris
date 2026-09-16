using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Tests.Settings;

public sealed class SyncAwxBlueprintHandlerTests
{
    private sealed class FakeRemoteCommandRunner : IRemoteCommandRunner
    {
        public RemoteCommandTarget? LastTarget { get; private set; }

        public string? LastCommand { get; private set; }

        public Func<RemoteCommandResult> OnRun { get; set; } = () => new RemoteCommandResult(0, "PLAY RECAP", string.Empty, false);

        public Task<RemoteCommandResult> RunAsync(
            RemoteCommandTarget target, string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            LastCommand = command;
            return Task.FromResult(OnRun());
        }
    }

    private static SyncAwxBlueprintHandler Handler(FakeStore store, FakeRemoteCommandRunner runner) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, runner);

    private static async Task<FakeStore> SeededStoreAsync()
    {
        var store = new FakeStore();
        await new SaveOpsHostIntegrationSettingsHandler(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader)
            .HandleAsync(new SaveOpsHostIntegrationSettingsCommand(
                "opsserver.internal", 2222, "ops", "SshKey", "-----BEGIN KEY-----", "/home/ops/Refactoring_ops_flow/awx"))
            .ConfigureAwait(false);
        return store;
    }

    [Fact]
    public async Task Requires_the_ops_host_to_be_configured()
    {
        var store = new FakeStore();
        var runner = new FakeRemoteCommandRunner();

        await Assert.ThrowsAsync<ValidationException>(() => Handler(store, runner).HandleAsync(new SyncAwxBlueprintCommand()));
        Assert.Null(runner.LastCommand);
    }

    [Fact]
    public async Task Runs_the_exact_command_an_operator_would_run_by_hand()
    {
        var store = await SeededStoreAsync();
        var runner = new FakeRemoteCommandRunner();

        var result = await Handler(store, runner).HandleAsync(new SyncAwxBlueprintCommand());

        Assert.True(result.Succeeded);
        Assert.Equal(
            "cd '/home/ops/Refactoring_ops_flow/awx' && ansible-playbook playbooks/ops/ops_awx_sync_blueprint.yml",
            runner.LastCommand);
        Assert.Equal("opsserver.internal", runner.LastTarget!.Host);
        Assert.Equal(2222, runner.LastTarget.Port);
        Assert.Equal("ops", runner.LastTarget.Username);
        Assert.Equal(ServerCredentialAuthMethod.SshKey, runner.LastTarget.AuthMethod);
        Assert.Equal("-----BEGIN KEY-----", runner.LastTarget.Secret);
    }

    [Fact]
    public async Task A_non_zero_exit_code_surfaces_as_a_validation_error_with_the_error_tail()
    {
        var store = await SeededStoreAsync();
        var runner = new FakeRemoteCommandRunner
        {
            OnRun = () => new RemoteCommandResult(1, string.Empty, "fatal: [localhost]: FAILED!", false),
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Handler(store, runner).HandleAsync(new SyncAwxBlueprintCommand()));

        Assert.Contains("fatal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_timeout_surfaces_as_a_validation_error()
    {
        var store = await SeededStoreAsync();
        var runner = new FakeRemoteCommandRunner
        {
            OnRun = () => new RemoteCommandResult(-1, string.Empty, string.Empty, true),
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Handler(store, runner).HandleAsync(new SyncAwxBlueprintCommand()));

        Assert.Contains("Timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
