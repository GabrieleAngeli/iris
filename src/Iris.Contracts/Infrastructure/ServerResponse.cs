namespace Iris.Contracts.Infrastructure;

/// <summary>One OS-login account on a server. Never carries a secret value.</summary>
public sealed record ServerCredentialResponse(
    Guid Id,
    string Username,
    string AuthMethod,
    string Kind,
    Guid? OwnerUserId,
    string? OwnerDisplayName,
    string? ServiceName,
    string? Label);

/// <summary>Resource hints for a server, as far as the operator knows them. Any field may be
/// unset. <c>FreeMemoryMb</c>/<c>FreeDiskGb</c> are only ever set by a real discovery — an
/// operator entering capacity by hand has no way to know "free" (they know what the box has,
/// not what's currently in use).</summary>
public sealed record ResourceProfileResponse(
    int? CpuCores,
    int? MemoryMb,
    int? DiskGb,
    int? ApplicationDiskGb,
    int? BackupDiskGb,
    int? FreeMemoryMb,
    int? FreeDiskGb);

/// <summary>One filesystem mount found on the server's last successful discovery.</summary>
public sealed record ServerDiskResponse(
    string DeviceName,
    string? MountPoint,
    string? FileSystem,
    int TotalGb,
    int FreeGb);

public sealed record ServerResponse(
    Guid Id,
    string Name,
    string? Hostname,
    string Os,
    string? OsVersion,
    string? MachineSize,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment,
    bool IsActive,
    IReadOnlyList<ServerCredentialResponse> Credentials,
    IReadOnlyList<string> Capabilities,
    ResourceProfileResponse? Resources,
    IReadOnlyList<int> UsedPorts,
    IReadOnlyList<ServerDiskResponse> Disks,
    bool? IsReachable,
    DateTimeOffset? LastDiscoveredAtUtc,
    string? LastDiscoveryError);

public sealed record DataServiceResponse(
    Guid Id,
    string Name,
    string Kind,
    string Endpoint,
    int? Port,
    string? Username,
    string? Version,
    string? Size,
    int? StorageGb,
    string Environment,
    bool IsActive);
