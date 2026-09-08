using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;

namespace Iris.Application.Tests.Settings;

public sealed class ProvisionOpenBaoHandlerTests
{
    private static ProvisionOpenBaoHandler Handler(FakeStore store, FakeContainerRuntime runtime) =>
        new(runtime, new SaveOpenBaoIntegrationSettingsHandler(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork));

    [Theory]
    [InlineData("==> OpenBao server started!\nRoot Token: s.abc123\n", "s.abc123")]
    [InlineData("Unseal Key: xyz\nRoot Token: s.with-trailing-space   \nCluster: foo", "s.with-trailing-space")]
    [InlineData("root token: s.case-insensitive-marker", "s.case-insensitive-marker")]
    public void ParseRootToken_finds_the_token_line(string logs, string expected)
    {
        Assert.Equal(expected, ProvisionOpenBaoHandler.ParseRootToken(logs));
    }

    /// <summary>
    /// Captured verbatim from `docker logs iris-openbao` on 2026-09-08, running the real
    /// `openbao/openbao:2.1` image via <c>ProvisionOpenBaoHandler.HandleAsync</c> against a real
    /// Docker daemon — the manual end-to-end check the class doc-comment on
    /// <see cref="ProvisionOpenBaoHandler"/> said hadn't happened yet. The banner also contains
    /// "root token"/"Root token" in unrelated sentences (see the "core: root token generated"
    /// and prose lines below) — this fixture is what proves the marker match doesn't false-positive
    /// on those and finds only the real "Root Token: " line.
    /// </summary>
    private const string RealOpenBaoDevModeLogs = """
        ==> OpenBao server configuration:

        Administrative Namespace:
                     Api Address: http://0.0.0.0:8200
                             Cgo: disabled
                 Cluster Address: https://0.0.0.0:8201
           Environment Variables: HOME, HOSTNAME, NAME, PATH, PWD, SHLVL, VERSION
                      Go Version: go1.23.5
                      Listener 1: tcp (addr: "0.0.0.0:8200", cluster address: "0.0.0.0:8201", max_request_duration: "1m30s", max_request_size: "33554432", tls: "disabled")
                       Log Level:
                   Recovery Mode: false
                         Storage: inmem
                         Version: OpenBao v2.1.1, built 2025-01-21T21:25:50Z
                     Version Sha: 17509a8c5e0af4ff921d4e70b06224397c44dd74

        ==> OpenBao server started! Log data will stream in below:

        2026-09-08T07:28:38.500Z [INFO]  core: security barrier initialized: stored=1 shares=1 threshold=1
        2026-09-08T07:28:38.521Z [INFO]  core: root token generated
        2026-09-08T07:28:38.522Z [INFO]  core: pre-seal teardown starting
        WARNING! dev mode is enabled! In this mode, OpenBao runs entirely in-memory
        and starts unsealed with a single unseal key. The root token is already
        authenticated to the CLI, so you can immediately begin using OpenBao.

        You may need to set the following environment variables:

            $ export BAO_ADDR='http://0.0.0.0:8200'

        The unseal key and root token are displayed below in case you want to
        seal/unseal the Vault or re-authenticate.

        Unseal Key: fvchIAmMmk1h9WNiSHxir2GMIyAKTEDpovJLM5Zko4E=
        Root Token: s.oi9YVUH2BqOJdjRl6BQKpOme

        Development mode should NOT be used in production installations!

        """;

    [Fact]
    public void ParseRootToken_finds_the_token_in_a_real_captured_OpenBao_banner()
    {
        Assert.Equal("s.oi9YVUH2BqOJdjRl6BQKpOme", ProvisionOpenBaoHandler.ParseRootToken(RealOpenBaoDevModeLogs));
    }

    [Fact]
    public void ParseRootToken_returns_null_when_no_token_line_exists()
    {
        Assert.Null(ProvisionOpenBaoHandler.ParseRootToken("==> OpenBao server started, nothing else here.\n"));
    }

    [Fact]
    public void ParseRootToken_returns_the_last_match_not_the_first()
    {
        // `docker logs` returns the container's entire history across every start — after a
        // stop+restart (see HandleAsync_restarts_a_stopped_container_... below) the log holds
        // both the old banner and the new one appended after it. The old token is no longer
        // valid; only the most recent one should ever be returned.
        var logs = "Root Token: s.stale-from-before-stop\n...\nRoot Token: s.fresh-after-restart\n";

        Assert.Equal("s.fresh-after-restart", ProvisionOpenBaoHandler.ParseRootToken(logs));
    }

    [Fact]
    public async Task HandleAsync_throws_when_docker_is_not_available()
    {
        var store = new FakeStore();
        var runtime = new FakeContainerRuntime { IsAvailable = false };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, runtime).HandleAsync(new ProvisionOpenBaoCommand()));
        Assert.Contains("Docker", ex.Message);
        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task HandleAsync_runs_a_new_container_and_persists_the_parsed_token_when_absent()
    {
        var store = new FakeStore();
        var runtime = new FakeContainerRuntime
        {
            IsAvailable = true,
            Status = new ContainerStatus(ContainerState.Absent, null),
            Logs = "==> OpenBao server started!\nRoot Token: s.fresh-token\n",
        };

        var result = await Handler(store, runtime).HandleAsync(new ProvisionOpenBaoCommand());

        Assert.Equal("http://localhost:8200", result.Endpoint);
        Assert.True(result.RestartRequired);
        Assert.Single(runtime.RunCalls);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("http://localhost:8200", settings.OpenBaoEndpoint);
        Assert.Equal("s.fresh-token", store.SecretsByReference[settings.OpenBaoTokenSecretReference!]);
    }

    [Fact]
    public async Task HandleAsync_reads_logs_directly_without_re_running_when_already_running()
    {
        var store = new FakeStore();
        var runtime = new FakeContainerRuntime
        {
            IsAvailable = true,
            Status = new ContainerStatus(ContainerState.Running, "existing-id"),
            Logs = "Root Token: s.already-running\n",
        };

        var result = await Handler(store, runtime).HandleAsync(new ProvisionOpenBaoCommand());

        Assert.Equal("http://localhost:8200", result.Endpoint);
        Assert.Empty(runtime.RunCalls);
    }

    [Fact]
    public async Task HandleAsync_restarts_a_stopped_container_and_persists_the_freshly_parsed_token()
    {
        // Regression test for a real bug found via manual testing (2026-09-08): "install for
        // me" used to hard-fail here and tell the operator to `docker rm` it by hand. Dev-mode
        // OpenBao keeps no state across restarts, so restarting is always safe — and the log
        // fixture below (old + new banner concatenated, exactly like real `docker logs` after a
        // restart) proves the freshly generated token wins over the stale one.
        var store = new FakeStore();
        var runtime = new FakeContainerRuntime
        {
            IsAvailable = true,
            Status = new ContainerStatus(ContainerState.Stopped, "existing-id"),
            Logs = "Root Token: s.stale-from-before-stop\nRoot Token: s.fresh-after-restart\n",
        };

        var result = await Handler(store, runtime).HandleAsync(new ProvisionOpenBaoCommand());

        Assert.Equal("http://localhost:8200", result.Endpoint);
        Assert.Equal(["iris-openbao"], runtime.StartCalls);
        Assert.Empty(runtime.RunCalls);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("s.fresh-after-restart", store.SecretsByReference[settings.OpenBaoTokenSecretReference!]);
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_root_token_cannot_be_found()
    {
        var store = new FakeStore();
        var runtime = new FakeContainerRuntime
        {
            IsAvailable = true,
            Status = new ContainerStatus(ContainerState.Absent, null),
            Logs = "==> OpenBao server started, but no token line.\n",
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, runtime).HandleAsync(new ProvisionOpenBaoCommand()));
        Assert.Contains("docker logs", ex.Message);
        Assert.Empty(store.IntegrationSettings);
    }

    private sealed class FakeContainerRuntime : IContainerRuntime
    {
        public bool IsAvailable { get; set; } = true;

        public ContainerStatus Status { get; set; } = new(ContainerState.Absent, null);

        public string Logs { get; set; } = string.Empty;

        public List<ContainerRunSpec> RunCalls { get; } = [];

        public List<string> StartCalls { get; } = [];

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsAvailable);

        public Task<ContainerStatus> GetStatusAsync(string containerName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Status);

        public Task<string> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken = default)
        {
            RunCalls.Add(spec);
            return Task.FromResult("fake-container-id");
        }

        public Task StartAsync(string containerName, CancellationToken cancellationToken = default)
        {
            StartCalls.Add(containerName);
            return Task.CompletedTask;
        }

        public Task<string> GetLogsAsync(string containerName, CancellationToken cancellationToken = default) => Task.FromResult(Logs);
    }
}
