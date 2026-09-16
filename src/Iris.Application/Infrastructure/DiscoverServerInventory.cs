using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>POST /servers/{serverId}/discover</c>.</summary>
public sealed record DiscoverServerInventoryCommand(Guid ServerId);

public sealed class DiscoverServerInventoryHandler(
    IServerRepository servers,
    IUserRepository users,
    ICustomerRepository customers,
    IEnvironmentServerAssignmentRepository environmentServerAssignments,
    IServerInventoryProbe inventoryProbe,
    IClock clock,
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

        var awxContextName = await ResolveAwxContextNameAsync(server.Id, cancellationToken).ConfigureAwait(false);
        var awxJobTemplateName = awxContextName is null ? null : $"{awxContextName}-facts";

        var snapshot = await inventoryProbe.DiscoverAsync(server, awxJobTemplateName, cancellationToken).ConfigureAwait(false);
        server.ApplyInventoryDiscovery(
            snapshot.IsReachable,
            clock.UtcNow,
            snapshot.Error,
            snapshot.Os,
            snapshot.OsVersion,
            snapshot.MachineSize,
            snapshot.Capabilities,
            snapshot.Resources,
            snapshot.UsedPorts,
            snapshot.Disks);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ownerNames = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        return server.ToResponse(ownerNames);
    }

    /// <summary>Resolves the single AWX context name this server unambiguously belongs to, or
    /// null when there's none (never assigned, no context has an AWX name) or more than one
    /// distinct name (a shared server spanning contexts) — either way, the caller falls back to
    /// the global job template id, exactly as before this resolution existed.</summary>
    private async Task<string?> ResolveAwxContextNameAsync(Guid serverNodeId, CancellationToken cancellationToken)
    {
        var assignments = await environmentServerAssignments.GetForServerAsync(serverNodeId, cancellationToken).ConfigureAwait(false);
        if (assignments.Count == 0)
        {
            return null;
        }

        var allCustomers = await customers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var contextsById = allCustomers
            .SelectMany(customer => customer.Contexts)
            .ToDictionary(context => context.Id, context => context);

        var awxNames = assignments
            .Select(assignment => contextsById.GetValueOrDefault(assignment.CustomerContextId)?.AwxContextName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return awxNames.Count == 1 ? awxNames[0] : null;
    }
}
