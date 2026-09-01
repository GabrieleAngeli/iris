using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

public interface IServerRepository
{
    /// <summary>All servers with their credentials eagerly loaded (read-only).</summary>
    Task<IReadOnlyList<ServerNode>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>A single server with credentials, not change-tracked.</summary>
    Task<ServerNode?> GetAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>A single server with credentials, change-tracked for mutation.</summary>
    Task<ServerNode?> GetForUpdateAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task AddAsync(ServerNode server, CancellationToken cancellationToken = default);

    void Remove(ServerNode server);
}
