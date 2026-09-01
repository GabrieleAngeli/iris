namespace Iris.Contracts.Infrastructure;

/// <summary>Body of <c>POST /servers</c>.</summary>
public sealed record CreateServerRequest(
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment);

/// <summary>Body of <c>POST /servers/{serverId}/credentials</c>. <c>SecretValue</c> is write-only — never echoed back.</summary>
public sealed record AddServerCredentialRequest(
    string Username,
    string AuthMethod,
    string SecretValue,
    string? Label);
