using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;

namespace Iris.Infrastructure.Inventory;

/// <summary>
/// Deterministic stand-in for the future Ansible/SSH probe. It gives the UI and validation
/// pipeline a real contract without pretending to reach the server yet.
/// </summary>
internal sealed class MockServerInventoryProbe : IServerInventoryProbe
{
    public Task<ServerInventorySnapshot> DiscoverAsync(ServerNode server, CancellationToken cancellationToken = default)
    {
        var isWindows = server.Os == ServerOs.Windows;
        var resources = new ResourceProfile(
            cpuCores: 4,
            memoryMb: 8192,
            diskGb: 250,
            applicationDiskGb: 160,
            backupDiskGb: 60);

        var snapshot = new ServerInventorySnapshot(
            server.Os,
            isWindows ? "Windows Server 2022" : "Ubuntu 22.04 LTS",
            server.HostingType == ServerHostingType.Cloud ? "Standard_D4s_v5" : "4 vCPU / 8 GB RAM",
            [NodeCapability.ServiceHost],
            resources,
            isWindows ? [3389, 5985] : [22]);

        return Task.FromResult(snapshot);
    }
}
