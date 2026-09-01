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

/// <summary>Resource hints for a server, as far as the operator knows them. Any field may be unset.</summary>
public sealed record ResourceProfileResponse(int? CpuCores, int? MemoryMb, int? DiskGb);

public sealed record ServerResponse(
    Guid Id,
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment,
    bool IsActive,
    IReadOnlyList<ServerCredentialResponse> Credentials,
    IReadOnlyList<string> Capabilities,
    ResourceProfileResponse? Resources,
    IReadOnlyList<int> UsedPorts);
