using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record ExecutePreparedActionCommand(Guid PreparedActionId);

/// <summary>
/// Step 3 of Prepare → Review → Execute: the operator's confirmation. Re-validates the installation
/// fresh (not from the frozen Prepare-time snapshot, since it may have changed since) and blocks if
/// it now has errors — otherwise delegates to the existing, unchanged
/// <see cref="LaunchApplicationInstallationAwxJobHandler"/> using the launch options the operator
/// reviewed, and links the resulting <see cref="InstallationRun"/> back onto the action.
/// </summary>
public sealed class ExecutePreparedActionHandler(
    IPreparedActionRepository actions,
    ValidateApplicationInstallationHandler validator,
    LaunchApplicationInstallationAwxJobHandler launcher,
    IInstallationRunRepository runs,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<PreparedActionResponse> HandleAsync(
        ExecutePreparedActionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var action = await actions.GetForUpdateAsync(command.PreparedActionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Prepared action", command.PreparedActionId);
        if (action.Status != PreparedActionStatus.Prepared)
        {
            throw new ValidationException(
                $"This action is already {action.Status.ToString().ToLowerInvariant()} and cannot be executed again.");
        }

        var validation = await validator
            .HandleAsync(new ValidateApplicationInstallationQuery(action.ApplicationInstallationId), cancellationToken)
            .ConfigureAwait(false);
        if (validation.Errors > 0)
        {
            throw new ValidationException(
                $"The installation now has {validation.Errors} validation error(s) that were not accounted for " +
                "when this action was prepared — cancel it and prepare again.");
        }

        var launchRequest = new ApplicationInstallationAwxLaunchRequest(
            action.RequestedJobTemplateId, action.RequestedInventory, action.RequestedLimit, action.RequestedCheckMode);
        var launch = await launcher
            .HandleAsync(new LaunchApplicationInstallationAwxJobCommand(action.ApplicationInstallationId, launchRequest), cancellationToken)
            .ConfigureAwait(false);

        action.MarkExecuted(launch.RunId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var run = await runs.GetAsync(launch.RunId, cancellationToken).ConfigureAwait(false);
        return action.ToResponse(action.DeserializePlan(), validation, run?.ToResponse());
    }
}
