using Iris.Application.Abstractions;

namespace Iris.Api.Diagnostics;

/// <summary>
/// Periodically calls <see cref="IIntegrationHealthChecker.RunOnceAsync"/> — a real
/// (<c>probe: true</c>) reachability check against every registered integration connector
/// (openbao/awx/ansible today) — so System settings/Dashboard reflect actual connectivity
/// without a live network call on every page load. First background/scheduled service in this
/// repo, requested by the user (2026-09-08): "dovrebbe esserci un servizio che controlla i
/// servizi connessi se sono raggiungibili e configurati correttamente."
///
/// Deliberately thin: all the actual checking logic (per-connector isolation, error handling)
/// lives in <c>IntegrationHealthChecker</c> (<c>Iris.Infrastructure</c>), tested there without
/// needing a real timer. This class is just the loop, not unit-tested itself — same as this
/// repo's other thin framework-glue classes (e.g. <c>SystemProcessRunner</c>'s raw
/// <c>Process.Start</c> call).
///
/// Deliberately separate from the built-in ASP.NET Core health check middleware/<c>/health</c>
/// endpoint (already registered in Program.cs) — that endpoint answers "is Iris.Api itself
/// alive" for infra/orchestrator liveness probes. A downed OpenBao must not make Iris.Api's own
/// health check fail and risk an unrelated container restart; this is a purely informational
/// signal for operators.
/// </summary>
internal sealed class IntegrationHealthCheckBackgroundService(
    IIntegrationHealthChecker checker,
    IConfiguration configuration,
    ILogger<IntegrationHealthCheckBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("Iris:HealthCheck:IntervalMinutes", 5);
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await checker.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // IntegrationHealthChecker already isolates per-connector failures — this is a
                // last-resort net so a genuinely unexpected exception never kills the loop for
                // the rest of this process's life.
                logger.LogError(ex, "Integration health check cycle failed unexpectedly");
            }
        }
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
