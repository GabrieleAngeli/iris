using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

internal static class ServerMapping
{
    public static ServerResponse ToResponse(
        this ServerNode server,
        IReadOnlyDictionary<Guid, string>? ownerNames = null) => new(
        server.Id,
        server.Name,
        server.Hostname,
        server.Os.ToString(),
        server.OsVersion,
        server.MachineSize,
        server.HostingType.ToString(),
        server.PublicIpAddress,
        server.PrivateIpAddress,
        server.Environment.ToString(),
        server.IsActive,
        server.Credentials.Select(c => c.ToResponse(LookupOwner(c, ownerNames))).ToArray(),
        server.Capabilities.Select(c => c.ToString()).ToArray(),
        server.Resources?.ToResponse(),
        server.UsedPorts,
        server.Disks.Select(d => d.ToResponse()).ToArray(),
        server.IsReachable,
        server.LastDiscoveredAtUtc,
        server.LastDiscoveryError);

    public static ResourceProfileResponse ToResponse(this ResourceProfile resources) => new(
        resources.CpuCores,
        resources.MemoryMb,
        resources.DiskGb,
        resources.ApplicationDiskGb,
        resources.BackupDiskGb,
        resources.FreeMemoryMb,
        resources.FreeDiskGb);

    public static ServerDiskResponse ToResponse(this ServerDisk disk) => new(
        disk.DeviceName,
        disk.MountPoint,
        disk.FileSystem,
        disk.TotalGb,
        disk.FreeGb);

    public static ServerCredentialResponse ToResponse(this ServerCredential credential, string? ownerDisplayName = null) => new(
        credential.Id,
        credential.Username,
        credential.AuthMethod.ToString(),
        credential.Kind.ToString(),
        credential.OwnerUserId,
        ownerDisplayName,
        credential.ServiceName,
        credential.Label);

    private static string? LookupOwner(ServerCredential credential, IReadOnlyDictionary<Guid, string>? ownerNames)
    {
        if (credential.OwnerUserId is { } id && ownerNames is not null && ownerNames.TryGetValue(id, out var name))
        {
            return name;
        }

        return null;
    }
}
