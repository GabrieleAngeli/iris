namespace Iris.Contracts.Infrastructure;

/// <summary>
/// An OS-login credential supplied by the operator. <c>SecretValue</c> is write-only (a password or an
/// SSH private key) and is never echoed back. <c>Kind</c> is <c>SystemUser</c> (optionally tied to an
/// Iris user via <c>OwnerUserId</c>) or <c>ServiceAccount</c> (named by <c>ServiceName</c>, e.g. "ansible").
/// </summary>
public sealed record ServerCredentialInputRequest(
    string Username,
    string AuthMethod,
    string SecretValue,
    string Kind,
    Guid? OwnerUserId,
    string? ServiceName,
    string? Label);

/// <summary>Body of <c>POST /servers</c>. <c>Credential</c> is optional — the server's first OS login.</summary>
public sealed record CreateServerRequest(
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment,
    ServerCredentialInputRequest? Credential = null);

/// <summary>Body of <c>PUT /servers/{id}</c> — the server's identity/network details (credentials unchanged).</summary>
public sealed record UpdateServerRequest(
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment);

/// <summary>Body of <c>POST /servers/{serverId}/credentials</c>.</summary>
public sealed record AddServerCredentialRequest(
    string Username,
    string AuthMethod,
    string SecretValue,
    string Kind,
    Guid? OwnerUserId,
    string? ServiceName,
    string? Label);

/// <summary>Resource hints supplied by the operator. Any field may be omitted.</summary>
public sealed record ResourceProfileRequest(int? CpuCores, int? MemoryMb, int? DiskGb);

/// <summary>
/// Body of <c>PUT /servers/{serverId}/capacity</c> — replaces the server's capability tags,
/// resource hints and known used ports wholesale.
/// </summary>
public sealed record UpdateServerCapacityRequest(
    IReadOnlyList<string> Capabilities,
    ResourceProfileRequest? Resources,
    IReadOnlyList<int> UsedPorts);
