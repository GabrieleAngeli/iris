using Iris.Application.Applications;

namespace Iris.Api.Diagnostics;

/// <summary>
/// Periodically refreshes every non-terminal <c>InstallationRun</c> from AWX
/// (<see cref="PollActiveInstallationRunsHandler"/>), so status/duration/log are kept current
/// without a human needing to open the run detail (which today is the only thing that triggers a
/// refresh — see <c>GetInstallationRunHandler</c>). Same thin-loop shape as
/// <see cref="IntegrationHealthCheckBackgroundService"/>, but this is the first background service
/// in the repo whose actual work is scoped (DbContext-backed
/// <c>IInstallationRunRepository</c>/<c>IUnitOfWork</c>) rather than singleton — so it resolves a
/// fresh <see cref="IServiceScope"/> on every tick instead of taking its dependency at the
/// constructor.
/// </summary>
internal sealed class InstallationRunPollingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<InstallationRunPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Iris:ActionsPolling:IntervalSeconds", 20);
        var interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<PollActiveInstallationRunsHandler>();
                await handler.HandleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // PollActiveInstallationRunsHandler already isolates per-run failures — this is a
                // last-resort net so a genuinely unexpected exception never kills the loop for the
                // rest of this process's life.
                logger.LogError(ex, "Installation run polling cycle failed unexpectedly");
            }
        }
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
