using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>PUT /servers/{id}</c> — the server's identity/network details.</summary>
public sealed record UpdateServerCommand(
    Guid Id,
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment);

public sealed class UpdateServerHandler(IServerRepository servers, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<ServerResponse> HandleAsync(UpdateServerCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var details = new ServerDetailsInput(
            command.Name, command.Hostname, command.Os, command.HostingType,
            command.PublicIpAddress, command.PrivateIpAddress, command.Environment);
        var (os, hostingType, environment) = details.Parse();

        var server = await servers.GetForUpdateAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.Id);

        server.UpdateDetails(
            command.Name, command.Hostname, os, hostingType,
            command.PublicIpAddress, command.PrivateIpAddress, environment);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ownerNames = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        return server.ToResponse(ownerNames);
    }
}
