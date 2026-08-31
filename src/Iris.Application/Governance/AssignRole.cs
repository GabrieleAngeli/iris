using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Access;
using Iris.Domain.Access;

namespace Iris.Application.Governance;

/// <summary>Command for <c>POST /users/{userId}/assignments</c>.</summary>
public sealed record AssignRoleCommand(
    Guid UserId,
    string RoleKey,
    string ScopeType,
    Guid? CustomerId,
    Guid? ContextId);

public sealed class AssignRoleHandler(
    IUserRepository users,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IUnitOfWork unitOfWork)
{
    public async Task<AssignmentResponse> HandleAsync(
        AssignRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.UserId);

        var role = await roles.GetByKeyAsync(command.RoleKey.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Role", command.RoleKey);

        var scope = ScopeFactory.FromParts(command.ScopeType, command.CustomerId, command.ContextId);

        if (await assignments.ExistsAsync(user.Id, role.Id, scope, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException(
                $"User already holds role '{role.Key}' at scope {scope}.");
        }

        var assignment = new RoleAssignment(Guid.CreateVersion7(), user.Id, role.Id, scope);
        await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AssignmentResponse(
            assignment.Id,
            user.Id,
            role.Key,
            scope.Type.ToString(),
            scope.CustomerId,
            scope.ContextId);
    }
}
