using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

public sealed record GetPreparedActionQuery(Guid PreparedActionId);

/// <summary>Detail view for the Actions page: the frozen plan/validation the operator reviewed, plus
/// the linked <see cref="Iris.Domain.Applications.InstallationRun"/> once executed.</summary>
public sealed class GetPreparedActionHandler(
    IPreparedActionRepository actions,
    IInstallationRunRepository runs)
{
    public async Task<PreparedActionResponse> HandleAsync(
        GetPreparedActionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var action = await actions.GetAsync(query.PreparedActionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Prepared action", query.PreparedActionId);

        InstallationRunResponse? run = null;
        if (action.InstallationRunId is { } runId)
        {
            var installationRun = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
            run = installationRun?.ToResponse();
        }

        return action.ToResponse(action.DeserializePlan(), action.DeserializeValidation(), run);
    }
}
