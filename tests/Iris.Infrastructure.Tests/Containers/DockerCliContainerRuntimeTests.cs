using Iris.Application.Abstractions;
using Iris.Infrastructure.Containers;
using Iris.Infrastructure.Processes;
using Iris.Infrastructure.Tests.Processes;

namespace Iris.Infrastructure.Tests.Containers;

public sealed class DockerCliContainerRuntimeTests
{
    [Fact]
    public async Task IsAvailableAsync_true_when_docker_info_succeeds_with_output()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "27.1.1\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);

        Assert.True(await runtime.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_false_when_docker_info_fails()
    {
        // Exactly the real scenario hit while writing this: docker CLI present, daemon not running.
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(1, "", "error during connect") };
        var runtime = new DockerCliContainerRuntime(runner);

        Assert.False(await runtime.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_false_when_docker_is_not_on_PATH_at_all()
    {
        // SystemProcessRunner maps a failed Process.Start to exit code -1 with no output.
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(-1, "", "The system cannot find the file specified") };
        var runtime = new DockerCliContainerRuntime(runner);

        Assert.False(await runtime.IsAvailableAsync());
    }

    [Fact]
    public async Task GetStatusAsync_reports_absent_when_the_command_finds_nothing()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "", "") };
        var runtime = new DockerCliContainerRuntime(runner);

        var status = await runtime.GetStatusAsync("iris-openbao");

        Assert.Equal(ContainerState.Absent, status.State);
        Assert.Null(status.ContainerId);
    }

    [Fact]
    public async Task GetStatusAsync_parses_a_running_container()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "abc123|Up 2 minutes (running)\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);

        var status = await runtime.GetStatusAsync("iris-openbao");

        Assert.Equal(ContainerState.Running, status.State);
        Assert.Equal("abc123", status.ContainerId);
    }

    [Fact]
    public async Task GetStatusAsync_parses_a_stopped_container()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "abc123|Exited (0) 3 minutes ago\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);

        var status = await runtime.GetStatusAsync("iris-openbao");

        Assert.Equal(ContainerState.Stopped, status.State);
    }

    [Fact]
    public async Task RunAsync_builds_the_expected_docker_command_line()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "container-id-123\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);
        var spec = new ContainerRunSpec(
            "iris-openbao",
            "openbao/openbao:2.1",
            new Dictionary<int, int> { [8200] = 8200 },
            ["server", "-dev"],
            ExtraArgs: ["--cap-add=IPC_LOCK"]);

        var containerId = await runtime.RunAsync(spec);

        Assert.Equal("container-id-123", containerId);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("docker", call.FileName);
        Assert.Equal(
            new[] { "run", "-d", "--name", "iris-openbao", "-p", "8200:8200", "--cap-add=IPC_LOCK", "openbao/openbao:2.1", "server", "-dev" },
            call.Arguments);
    }

    [Fact]
    public async Task RunAsync_includes_environment_variables_when_given()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "id\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);
        var spec = new ContainerRunSpec(
            "name", "image", new Dictionary<int, int>(), ["cmd"],
            EnvironmentVariables: new Dictionary<string, string> { ["FOO"] = "bar" });

        await runtime.RunAsync(spec);

        var call = Assert.Single(runner.Calls);
        Assert.Contains("-e", call.Arguments);
        Assert.Contains("FOO=bar", call.Arguments);
    }

    [Fact]
    public async Task RunAsync_throws_with_the_docker_error_when_the_command_fails()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(1, "", "Error: port is already allocated") };
        var runtime = new DockerCliContainerRuntime(runner);
        var spec = new ContainerRunSpec("iris-openbao", "openbao/openbao:2.1", new Dictionary<int, int>(), ["server", "-dev"]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(spec));
        Assert.Contains("port is already allocated", ex.Message);
    }

    [Fact]
    public async Task StartAsync_runs_docker_start_with_the_container_name()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "iris-openbao\n", "") };
        var runtime = new DockerCliContainerRuntime(runner);

        await runtime.StartAsync("iris-openbao");

        var call = Assert.Single(runner.Calls);
        Assert.Equal("docker", call.FileName);
        Assert.Equal(new[] { "start", "iris-openbao" }, call.Arguments);
    }

    [Fact]
    public async Task StartAsync_throws_with_the_docker_error_when_the_command_fails()
    {
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(1, "", "Error: No such container: iris-openbao") };
        var runtime = new DockerCliContainerRuntime(runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync("iris-openbao"));
        Assert.Contains("No such container", ex.Message);
    }

    [Fact]
    public async Task GetLogsAsync_concatenates_stdout_and_stderr()
    {
        // OpenBao's dev-mode banner (including the root token line) lands on stdout or stderr
        // depending on version — this must not assume either one.
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "stdout line", "stderr line") };
        var runtime = new DockerCliContainerRuntime(runner);

        var logs = await runtime.GetLogsAsync("iris-openbao");

        Assert.Contains("stdout line", logs);
        Assert.Contains("stderr line", logs);
    }
}
