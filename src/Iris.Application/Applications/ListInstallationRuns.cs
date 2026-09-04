using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

public sealed record ListInstallationRunsQuery(Guid InstallationId);

public sealed class ListInstallationRunsHandler(
    IApplicationInstallationRepository installations,
    IInstallationRunRepository runs)
{
    public async Task<IReadOnlyList<InstallationRunResponse>> HandleAsync(
        ListInstallationRunsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await installations.GetAsync(query.InstallationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application installation", query.InstallationId);

        var history = await runs.GetForInstallationAsync(query.InstallationId, cancellationToken).ConfigureAwait(false);
        return history.Select(run => run.ToResponse()).ToArray();
    }
}
