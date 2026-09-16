using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;

namespace Iris.Api.Tests;

/// <summary>
/// Stand-in wired into every <see cref="IrisApiFactory"/> so <c>POST /servers/{id}/discover</c>
/// never launches a real AWX job — there is no AWX in an automated test run. Deterministic, same
/// reasoning as <see cref="FakeSecretStorePromotion"/>/<see cref="FakeContainerRuntime"/>. Never
/// touches <c>UsedPorts</c> — it passes the server's own value through, same contract the real
/// <c>AwxServerInventoryProbe</c> honors.
/// </summary>
internal sealed class FakeServerInventoryProbe : IServerInventoryProbe
{
    public Task<ServerInventorySnapshot> DiscoverAsync(
        ServerNode server, string? awxJobTemplateName = null, CancellationToken cancellationToken = default)
    {
        var isWindows = server.Os == ServerOs.Windows;
        var resources = new ResourceProfile(
            cpuCores: 4,
            memoryMb: 8192,
            diskGb: 250,
            applicationDiskGb: 160,
            backupDiskGb: 60,
            freeMemoryMb: 4096,
            freeDiskGb: 90);

        var snapshot = new ServerInventorySnapshot(
            IsReachable: true,
            Error: null,
            Os: server.Os,
            OsVersion: isWindows ? "Windows Server 2022" : "Ubuntu 22.04 LTS",
            MachineSize: server.HostingType == ServerHostingType.Cloud ? "Standard_D4s_v5" : "4 vCPU / 8 GB RAM",
            Capabilities: [NodeCapability.ServiceHost],
            Resources: resources,
            UsedPorts: server.UsedPorts,
            Disks: [new ServerDiskInput(isWindows ? "C:" : "/dev/sda1", isWindows ? "C:" : "/", isWindows ? "ntfs" : "ext4", 250, 90)]);

        return Task.FromResult(snapshot);
    }
}
