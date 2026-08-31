using Iris.Application.Abstractions;
using Iris.Contracts.Access;

namespace Iris.Application.Access;

/// <summary>Query for <c>GET /users</c>: every user with the roles they hold and where.</summary>
public sealed record ListUsersQuery;

public sealed class ListUsersHandler(
    IUserRepository users,
    IRoleAssignmentRepository assignments,
    IRoleRepository roles)
{
    public async Task<IReadOnlyList<UserResponse>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allUsers = await users.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var userIds = allUsers.Select(u => u.Id).ToArray();

        var allAssignments = await assignments.GetForUsersAsync(userIds, cancellationToken).ConfigureAwait(false);
        var rolesById = (await roles.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id);

        return allUsers
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new UserResponse(
                user.Id,
                user.ExternalId,
                user.Email,
                user.DisplayName,
                user.IsActive,
                allAssignments
                    .Where(a => a.UserId == user.Id)
                    .Select(a => new UserAssignmentDto(
                        a.Id,
                        rolesById.TryGetValue(a.RoleId, out var role) ? role.Key : "(unknown)",
                        rolesById.TryGetValue(a.RoleId, out role) ? role.Name : "(unknown)",
                        a.Scope.Type.ToString(),
                        a.Scope.CustomerId,
                        a.Scope.ContextId))
                    .ToArray()))
            .ToArray();
    }
}
