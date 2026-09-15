using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

/// <summary>
/// Outcome of one discovery attempt against a <see cref="ServerNode"/>. When
/// <see cref="IsReachable"/> is <c>false</c>, every field below <see cref="Error"/> is
/// meaningless — the caller keeps whatever was last discovered. <see cref="UsedPorts"/> is
/// always the server's own current value, unchanged: no probe implementation maps or scans
/// ports.
/// </summary>
public sealed record ServerInventorySnapshot(
    bool IsReachable,
    string? Error,
    ServerOs Os,
    string? OsVersion,
    string? MachineSize,
    IReadOnlyList<NodeCapability> Capabilities,
    ResourceProfile? Resources,
    IReadOnlyList<int> UsedPorts,
    IReadOnlyList<ServerDiskInput> Disks)
{
    public static ServerInventorySnapshot Unreachable(string error) =>
        new(false, error, default, null, null, [], null, [], []);
}

public interface IServerInventoryProbe
{
    Task<ServerInventorySnapshot> DiscoverAsync(ServerNode server, CancellationToken cancellationToken = default);
}
