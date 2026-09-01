using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

internal static class ServerMapping
{
    public static ServerResponse ToResponse(this ServerNode server) => new(
        server.Id,
        server.Name,
        server.Hostname,
        server.Os.ToString(),
        server.HostingType.ToString(),
        server.PublicIpAddress,
        server.PrivateIpAddress,
        server.Environment.ToString(),
        server.IsActive,
        server.Credentials.Select(c => c.ToResponse()).ToArray());

    public static ServerCredentialResponse ToResponse(this ServerCredential credential) => new(
        credential.Id,
        credential.Username,
        credential.AuthMethod.ToString(),
        credential.Label);
}
