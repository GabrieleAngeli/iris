using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>PUT /servers/{serverId}/capacity</c>.</summary>
public sealed record UpdateServerCapacityCommand(
    Guid ServerId,
    IReadOnlyList<string> Capabilities,
    ResourceProfileRequest? Resources,
    IReadOnlyList<int> UsedPorts);

public sealed class UpdateServerCapacityHandler(IServerRepository servers, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<ServerResponse> HandleAsync(
        UpdateServerCapacityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var capabilities = new List<NodeCapability>();
        foreach (var value in command.Capabilities)
        {
            if (!Enum.TryParse<NodeCapability>(value, ignoreCase: true, out var capability))
            {
                throw new ValidationException(
                    $"Unknown capability '{value}'. Expected LoadBalancer, Database, ServiceHost or Presentation.");
            }

            capabilities.Add(capability);
        }

        var resources = command.Resources is { } input
            ? new ResourceProfile(input.CpuCores, input.MemoryMb, input.DiskGb)
            : null;

        var server = await servers.GetForUpdateAsync(command.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerId);

        server.UpdateCapacity(capabilities, resources, command.UsedPorts);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ownerNames = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        return server.ToResponse(ownerNames);
    }
}
