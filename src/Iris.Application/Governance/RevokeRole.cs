using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Governance;

/// <summary>Command for <c>DELETE /users/{userId}/assignments/{assignmentId}</c>.</summary>
public sealed record RevokeRoleCommand(Guid UserId, Guid AssignmentId);

public sealed class RevokeRoleHandler(IRoleAssignmentRepository assignments, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(RevokeRoleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assignment = await assignments.GetAsync(command.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (assignment is null || assignment.UserId != command.UserId)
        {
            throw new NotFoundException("Role assignment", command.AssignmentId);
        }

        assignments.Remove(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
