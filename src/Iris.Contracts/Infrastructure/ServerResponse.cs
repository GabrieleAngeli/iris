namespace Iris.Contracts.Infrastructure;

/// <summary>One OS-login account on a server. Never carries a secret value.</summary>
public sealed record ServerCredentialResponse(
    Guid Id,
    string Username,
    string AuthMethod,
    string? Label);

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
    IReadOnlyList<ServerCredentialResponse> Credentials);
