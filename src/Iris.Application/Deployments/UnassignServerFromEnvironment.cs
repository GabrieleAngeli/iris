using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Deployments;

public sealed record UnassignServerFromEnvironmentCommand(Guid AssignmentId);

public sealed class UnassignServerFromEnvironmentHandler(
    IEnvironmentServerAssignmentRepository assignments,
    IApplicationInstallationRepository installations,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(
        UnassignServerFromEnvironmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assignment = await assignments.GetAsync(command.AssignmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Environment server assignment", command.AssignmentId);

        var allInstallations = await installations.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var stillInUse = allInstallations.Any(installation =>
            installation.CustomerContextId == assignment.CustomerContextId &&
            installation.ServerNodeId == assignment.ServerNodeId);
        if (stillInUse)
        {
            throw new ConflictException("This server still has application installations in this environment. Remove them before unassigning the server.");
        }

        assignments.Remove(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
