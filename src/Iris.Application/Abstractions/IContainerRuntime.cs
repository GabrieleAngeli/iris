namespace Iris.Application.Abstractions;

/// <summary>Whether a named container exists, and if so its last-known run state.</summary>
public enum ContainerState
{
    Absent,
    Running,
    Stopped,
}

public sealed record ContainerStatus(ContainerState State, string? ContainerId);

/// <summary>
/// A container to run: host-to-container port bindings, the command to run inside it, and
/// any extra flags <see cref="IContainerRuntime"/> doesn't have a dedicated parameter for
/// (kept as raw CLI-style strings deliberately — this port is a thin wrapper over "run a
/// container", not a general container-orchestration abstraction).
/// </summary>
public sealed record ContainerRunSpec(
    string Name,
    string Image,
    IReadOnlyDictionary<int, int> PortBindings,
    IReadOnlyList<string> Command,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    IReadOnlyList<string>? ExtraArgs = null);

/// <summary>
/// Runs containers on whatever container engine is available on the host Iris.Api itself
/// runs on — today Docker only (see <c>DockerCliContainerRuntime</c> in
/// <c>Iris.Infrastructure</c>). This is deliberately narrow (check availability, check one
/// named container's status, run one), built for self-provisioning a convenience OpenBao
/// instance (see <c>ProvisionOpenBaoHandler</c>) — not a general orchestration port.
/// </summary>
public interface IContainerRuntime
{
    /// <summary>True if the container engine itself is reachable (its CLI is on PATH and its
    /// daemon answers) — false, not a thrown exception, when it isn't; callers turn that into
    /// an operator-facing message rather than a crash.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<ContainerStatus> GetStatusAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>Starts <paramref name="spec"/> detached and returns its container id.</summary>
    Task<string> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Restarts a container that already exists but is stopped (<see cref="ContainerState.Stopped"/>)
    /// — same container, same image/config it was created with, no new <see cref="ContainerRunSpec"/>
    /// needed. Callers should not call this for <see cref="ContainerState.Absent"/> (use
    /// <see cref="RunAsync"/> instead) or <see cref="ContainerState.Running"/> (nothing to do).</summary>
    Task StartAsync(string containerName, CancellationToken cancellationToken = default);

    Task<string> GetLogsAsync(string containerName, CancellationToken cancellationToken = default);
}
