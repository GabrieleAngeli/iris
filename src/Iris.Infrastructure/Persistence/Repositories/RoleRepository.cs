using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository(IrisDbContext dbContext) : IRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetByIdsAsync(
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = roleIds as IReadOnlyCollection<Guid> ?? roleIds.ToArray();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Roles
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Roles
            .OrderBy(r => r.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        dbContext.Roles.SingleOrDefaultAsync(r => r.Key == key, cancellationToken);
}
