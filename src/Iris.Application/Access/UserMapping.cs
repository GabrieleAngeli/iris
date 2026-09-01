using Iris.Contracts.Access;
using Iris.Domain.Access;

namespace Iris.Application.Access;

internal static class UserMapping
{
    public static UserResponse ToResponse(
        User user,
        IEnumerable<RoleAssignment> assignments,
        IReadOnlyDictionary<Guid, Role> rolesById) => new(
        user.Id,
        user.ExternalId,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.IsProvisioned,
        assignments
            .Where(a => a.UserId == user.Id)
            .Select(a => new UserAssignmentDto(
                a.Id,
                rolesById.TryGetValue(a.RoleId, out var role) ? role.Key : "(unknown)",
                rolesById.TryGetValue(a.RoleId, out role) ? role.Name : "(unknown)",
                a.Scope.Type.ToString(),
                a.Scope.CustomerId,
                a.Scope.ContextId))
            .ToArray());
}
