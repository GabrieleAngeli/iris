using Iris.Application.Abstractions;
using Iris.Infrastructure.Processes;

namespace Iris.Infrastructure.Containers;

/// <summary>
/// Shells out to the <c>docker</c> CLI — the only implementation of <see cref="IContainerRuntime"/>
/// today. No Docker SDK/socket-client package is referenced deliberately: the CLI is already
/// present wherever Docker itself is, needs no extra dependency, and is what an operator would
/// run by hand to verify/undo anything this does.
/// </summary>
internal sealed class DockerCliContainerRuntime(IProcessRunner processRunner) : IContainerRuntime
{
    private const string DockerExecutable = "docker";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await processRunner
            .RunAsync(DockerExecutable, ["info", "--format", "{{.ServerVersion}}"], cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    public async Task<ContainerStatus> GetStatusAsync(string containerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        var result = await processRunner.RunAsync(
                DockerExecutable,
                ["ps", "-a", "--filter", $"name=^{containerName}$", "--format", "{{.ID}}|{{.State}}"],
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            return new ContainerStatus(ContainerState.Absent, null);
        }

        var line = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (line is null)
        {
            return new ContainerStatus(ContainerState.Absent, null);
        }

        var parts = line.Split('|', 2);
        var containerId = parts[0];
        var running = parts.Length > 1 && parts[1].Contains("running", StringComparison.OrdinalIgnoreCase);
        return new ContainerStatus(running ? ContainerState.Running : ContainerState.Stopped, containerId);
    }

    public async Task<string> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var arguments = new List<string> { "run", "-d", "--name", spec.Name };

        foreach (var (hostPort, containerPort) in spec.PortBindings)
        {
            arguments.Add("-p");
            arguments.Add($"{hostPort}:{containerPort}");
        }

        if (spec.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in spec.EnvironmentVariables)
            {
                arguments.Add("-e");
                arguments.Add($"{key}={value}");
            }
        }

        if (spec.ExtraArgs is not null)
        {
            arguments.AddRange(spec.ExtraArgs);
        }

        arguments.Add(spec.Image);
        arguments.AddRange(spec.Command);

        var result = await processRunner.RunAsync(DockerExecutable, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"'docker run' failed: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Trim();
    }

    public async Task StartAsync(string containerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        var result = await processRunner.RunAsync(DockerExecutable, ["start", containerName], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"'docker start' failed: {result.StandardError.Trim()}");
        }
    }

    public async Task<string> GetLogsAsync(string containerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        var result = await processRunner.RunAsync(DockerExecutable, ["logs", containerName], cancellationToken).ConfigureAwait(false);
        // OpenBao's dev-mode banner (including the root token line) goes to stderr on some
        // versions and stdout on others — concatenate both rather than guess which.
        return result.StandardOutput + "\n" + result.StandardError;
    }
}
