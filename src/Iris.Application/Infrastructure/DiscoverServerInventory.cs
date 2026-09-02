using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>POST /servers/{serverId}/discover</c>.</summary>
public sealed record DiscoverServerInventoryCommand(Guid ServerId);

public sealed class DiscoverServerInventoryHandler(
    IServerRepository servers,
    IUserRepository users,
    IServerInventoryProbe inventoryProbe,
    IUnitOfWork unitOfWork)
{
    public async Task<ServerResponse> HandleAsync(
        DiscoverServerInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var server = await servers.GetForUpdateAsync(command.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerId);

        if (server.Credentials.Count == 0)
        {
            throw new ValidationException("Add at least one server credential before discovering inventory.");
        }

        var snapshot = await inventoryProbe.DiscoverAsync(server, cancellationToken).ConfigureAwait(false);
        server.ApplyInventoryDiscovery(
            snapshot.Os,
            snapshot.OsVersion,
            snapshot.MachineSize,
            snapshot.Capabilities,
            snapshot.Resources,
            snapshot.UsedPorts);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ownerNames = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        return server.ToResponse(ownerNames);
    }
}
