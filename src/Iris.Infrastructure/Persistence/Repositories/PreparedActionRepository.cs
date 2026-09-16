using Iris.Application.Abstractions;
using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class PreparedActionRepository(IrisDbContext dbContext) : IPreparedActionRepository
{
    public async Task<IReadOnlyList<PreparedAction>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PreparedActions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<PreparedAction?> GetAsync(Guid actionId, CancellationToken cancellationToken = default) =>
        dbContext.PreparedActions
            .AsNoTracking()
            .SingleOrDefaultAsync(action => action.Id == actionId, cancellationToken);

    public Task<PreparedAction?> GetForUpdateAsync(Guid actionId, CancellationToken cancellationToken = default) =>
        dbContext.PreparedActions
            .SingleOrDefaultAsync(action => action.Id == actionId, cancellationToken);

    public async Task AddAsync(PreparedAction action, CancellationToken cancellationToken = default) =>
        await dbContext.PreparedActions.AddAsync(action, cancellationToken).ConfigureAwait(false);
}
