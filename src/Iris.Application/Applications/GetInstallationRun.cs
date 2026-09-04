using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record GetInstallationRunQuery(Guid InstallationId, Guid RunId);

public sealed class GetInstallationRunHandler(
    IInstallationRunRepository runs,
    IAwxClient awx,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<InstallationRunResponse> HandleAsync(
        GetInstallationRunQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = await runs.GetForUpdateAsync(query.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null || run.ApplicationInstallationId != query.InstallationId)
        {
            throw new NotFoundException("Installation run", query.RunId);
        }

        if (run.IsTerminal || run.Kind != InstallationRunKind.AwxJob || string.IsNullOrWhiteSpace(run.ExternalJobId))
        {
            return run.ToResponse();
        }

        try
        {
            var status = await awx.GetJobStatusAsync(run.ExternalJobId, cancellationToken).ConfigureAwait(false);
            run.UpdateStatus(InstallationRunMapping.FromAwxStatus(status.Status), status.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ValidationException)
        {
            // AWX not configured / unreachable — keep the last known status, do not fail the read.
        }

        return run.ToResponse();
    }
}
