using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

public sealed record ServerInventorySnapshot(
    ServerOs Os,
    string? OsVersion,
    string? MachineSize,
    IReadOnlyList<NodeCapability> Capabilities,
    ResourceProfile? Resources,
    IReadOnlyList<int> UsedPorts);

public interface IServerInventoryProbe
{
    Task<ServerInventorySnapshot> DiscoverAsync(ServerNode server, CancellationToken cancellationToken = default);
}
