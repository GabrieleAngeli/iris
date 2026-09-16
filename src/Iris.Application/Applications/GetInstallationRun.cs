using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record GetInstallationRunQuery(Guid InstallationId, Guid RunId);

public sealed class GetInstallationRunHandler(
    IInstallationRunRepository runs,
    IInstallationRunRefresher refresher,
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

        await refresher.RefreshAsync(run, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return run.ToResponse();
    }
}
