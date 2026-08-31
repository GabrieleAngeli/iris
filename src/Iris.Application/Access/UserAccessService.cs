using Iris.Application.Abstractions;

namespace Iris.Application.Access;

internal sealed class UserAccessService(
    IUserRepository users,
    IRoleAssignmentRepository assignments,
    IRoleRepository roles) : IUserAccessService
{
    public async Task<UserAccessSnapshot?> GetSnapshotAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var user = await users.FindByExternalIdAsync(externalId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var userAssignments = await assignments.GetForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var roleIds = userAssignments.Select(a => a.RoleId).Distinct().ToArray();
        var rolesById = (await roles.GetByIdsAsync(roleIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(r => r.Id);

        var views = new List<AssignmentView>(userAssignments.Count);
        foreach (var assignment in userAssignments)
        {
            if (!rolesById.TryGetValue(assignment.RoleId, out var role))
            {
                continue;
            }

            views.Add(new AssignmentView(
                role.Id,
                role.Key,
                role.Name,
                assignment.Scope,
                role.Permissions.ToArray()));
        }

        return new UserAccessSnapshot(user.Id, user.ExternalId, user.Email, user.DisplayName, views);
    }
}
