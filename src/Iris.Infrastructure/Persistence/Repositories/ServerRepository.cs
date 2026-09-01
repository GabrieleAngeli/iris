using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class ServerRepository(IrisDbContext dbContext) : IServerRepository
{
    public async Task<IReadOnlyList<ServerNode>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Servers
            .Include(s => s.Credentials)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ServerNode?> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        dbContext.Servers
            .Include(s => s.Credentials)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == serverId, cancellationToken);

    public Task<ServerNode?> GetForUpdateAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        dbContext.Servers
            .Include(s => s.Credentials)
            .SingleOrDefaultAsync(s => s.Id == serverId, cancellationToken);

    public async Task AddAsync(ServerNode server, CancellationToken cancellationToken = default) =>
        await dbContext.Servers.AddAsync(server, cancellationToken).ConfigureAwait(false);
}
