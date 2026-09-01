using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class EditLockRepository(IrisDbContext dbContext) : IEditLockRepository
{
    public Task<EditLock?> FindAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken = default) =>
        dbContext.Set<EditLock>()
            .SingleOrDefaultAsync(l => l.ResourceType == resourceType && l.ResourceId == resourceId, cancellationToken);

    public async Task AddAsync(EditLock editLock, CancellationToken cancellationToken = default) =>
        await dbContext.Set<EditLock>().AddAsync(editLock, cancellationToken).ConfigureAwait(false);

    public void Remove(EditLock editLock) => dbContext.Set<EditLock>().Remove(editLock);
}
