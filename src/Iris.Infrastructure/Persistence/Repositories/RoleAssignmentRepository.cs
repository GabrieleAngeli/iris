using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class RoleAssignmentRepository(IrisDbContext dbContext) : IRoleAssignmentRepository
{
    public async Task<IReadOnlyList<RoleAssignment>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.RoleAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<RoleAssignment>> GetForUsersAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds as IReadOnlyCollection<Guid> ?? userIds.ToArray();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.RoleAssignments
            .Where(a => ids.Contains(a.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<RoleAssignment?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.RoleAssignments.SingleOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid userId,
        Guid roleId,
        AccessScope scope,
        CancellationToken cancellationToken = default) =>
        dbContext.RoleAssignments.AnyAsync(
            a => a.UserId == userId
                && a.RoleId == roleId
                && a.Scope.Type == scope.Type
                && a.Scope.CustomerId == scope.CustomerId
                && a.Scope.ContextId == scope.ContextId,
            cancellationToken);

    public async Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken = default) =>
        await dbContext.RoleAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

    public void Remove(RoleAssignment assignment) => dbContext.RoleAssignments.Remove(assignment);
}
