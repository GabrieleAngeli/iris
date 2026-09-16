using Iris.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Iris.Application.Applications;

/// <summary>
/// The testable half of the background polling loop (the timer itself is a thin
/// <c>BackgroundService</c> in Iris.Api): refreshes every non-terminal <c>InstallationRun</c> from
/// AWX via <see cref="IInstallationRunRefresher"/>, one failing run isolated from the rest — same
/// per-item isolation as <c>IntegrationHealthChecker</c> uses for connectors.
/// </summary>
public sealed class PollActiveInstallationRunsHandler(
    IInstallationRunRepository runs,
    IInstallationRunRefresher refresher,
    IUnitOfWork unitOfWork,
    ILogger<PollActiveInstallationRunsHandler> logger)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var active = await runs.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var refreshedCount = 0;

        foreach (var run in active)
        {
            try
            {
                await refresher.RefreshAsync(run, cancellationToken).ConfigureAwait(false);
                refreshedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to refresh installation run {RunId}", run.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return refreshedCount;
    }
}
