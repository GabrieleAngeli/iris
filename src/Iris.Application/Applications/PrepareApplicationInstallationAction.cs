using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record PrepareApplicationInstallationActionCommand(
    Guid InstallationId,
    ApplicationInstallationAwxLaunchRequest? RequestedLaunchOptions);

/// <summary>
/// Step 1 of Prepare → Review → Execute: freezes a reviewable snapshot of the Ansible plan and
/// validation checks for an installation, before anything is sent to AWX. Always succeeds, even
/// when validation finds errors — seeing the risk is the point of Review;
/// <see cref="ExecutePreparedActionHandler"/> is what blocks execution on errors.
/// </summary>
public sealed class PrepareApplicationInstallationActionHandler(
    IApplicationInstallationRepository installations,
    ValidateApplicationInstallationHandler validator,
    GetApplicationInstallationAnsiblePlanHandler plans,
    IPreparedActionRepository actions,
    IUnitOfWork unitOfWork)
{
    public async Task<PreparedActionResponse> HandleAsync(
        PrepareApplicationInstallationActionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await installations.GetAsync(command.InstallationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application installation", command.InstallationId);

        var validation = await validator
            .HandleAsync(new ValidateApplicationInstallationQuery(command.InstallationId), cancellationToken)
            .ConfigureAwait(false);
        var plan = await plans
            .HandleAsync(new GetApplicationInstallationAnsiblePlanQuery(command.InstallationId), cancellationToken)
            .ConfigureAwait(false);

        var options = command.RequestedLaunchOptions ?? new ApplicationInstallationAwxLaunchRequest();
        var action = new PreparedAction(
            Guid.CreateVersion7(),
            command.InstallationId,
            JsonSerializer.Serialize(plan),
            JsonSerializer.Serialize(validation),
            options.JobTemplateId,
            options.Inventory,
            options.Limit,
            options.CheckMode);

        await actions.AddAsync(action, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return action.ToResponse(plan, validation, run: null);
    }
}
