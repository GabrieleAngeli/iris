using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IRoleAssignmentRepository
{
    Task<IReadOnlyList<RoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleAssignment>> GetForUsersAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<RoleAssignment?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid userId,
        Guid roleId,
        AccessScope scope,
        CancellationToken cancellationToken = default);

    Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken = default);

    void Remove(RoleAssignment assignment);
}
