using Iris.Application.Abstractions;
using Iris.Domain.Deployments;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class EnvironmentServerAssignmentRepository(IrisDbContext dbContext) : IEnvironmentServerAssignmentRepository
{
    public async Task<IReadOnlyList<EnvironmentServerAssignment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.EnvironmentServerAssignments
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<EnvironmentServerAssignment?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.EnvironmentServerAssignments
            .SingleOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken);

    public Task<bool> ExistsAsync(Guid customerContextId, Guid serverNodeId, CancellationToken cancellationToken = default) =>
        dbContext.EnvironmentServerAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.CustomerContextId == customerContextId && assignment.ServerNodeId == serverNodeId,
                cancellationToken);

    public async Task AddAsync(EnvironmentServerAssignment assignment, CancellationToken cancellationToken = default) =>
        await dbContext.EnvironmentServerAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

    public void Remove(EnvironmentServerAssignment assignment) => dbContext.EnvironmentServerAssignments.Remove(assignment);
}
