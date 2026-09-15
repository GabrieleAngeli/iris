using Iris.Domain.Common;

namespace Iris.Domain.Infrastructure;

/// <summary>
/// One filesystem mount discovered on a <see cref="ServerNode"/> — e.g. <c>/</c> on Linux or
/// <c>C:</c> on Windows. Owned by its <see cref="ServerNode"/>: no meaning on its own, replaced
/// wholesale on every successful discovery (see <see cref="ServerNode.ApplyInventoryDiscovery"/>).
/// </summary>
public sealed class ServerDisk : Entity<Guid>
{
    // For the persistence layer.
    private ServerDisk()
        : base(Guid.Empty)
    {
        DeviceName = string.Empty;
    }

    internal ServerDisk(
        Guid id,
        Guid serverNodeId,
        string deviceName,
        string? mountPoint,
        string? fileSystem,
        int totalGb,
        int freeGb)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        ServerNodeId = serverNodeId;
        DeviceName = deviceName.Trim();
        MountPoint = string.IsNullOrWhiteSpace(mountPoint) ? null : mountPoint.Trim();
        FileSystem = string.IsNullOrWhiteSpace(fileSystem) ? null : fileSystem.Trim();
        TotalGb = totalGb;
        FreeGb = freeGb;
    }

    public Guid ServerNodeId { get; private set; }

    public string DeviceName { get; private set; }

    public string? MountPoint { get; private set; }

    public string? FileSystem { get; private set; }

    public int TotalGb { get; private set; }

    public int FreeGb { get; private set; }
}

/// <summary>Input for <see cref="ServerNode.ApplyInventoryDiscovery"/> — one discovered mount,
/// before it's given an identity.</summary>
public sealed record ServerDiskInput(
    string DeviceName,
    string? MountPoint,
    string? FileSystem,
    int TotalGb,
    int FreeGb);
