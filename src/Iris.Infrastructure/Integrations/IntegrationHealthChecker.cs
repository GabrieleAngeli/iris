using Iris.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Real implementation of <see cref="IIntegrationHealthChecker"/> — probes every registered
/// <see cref="IIntegrationConnector"/> (openbao/awx/ansible today) with a real reachability check
/// (<c>probe: true</c>) and records the result into <see cref="IIntegrationHealthMonitor"/>.
///
/// Requested by the user (2026-09-08): "dovrebbe esserci un servizio che controlla i servizi
/// connessi se sono raggiungibili e configurati correttamente" — this is that check; the timer
/// that calls it periodically lives in <c>Iris.Api</c> (<c>IntegrationHealthCheckBackgroundService</c>),
/// deliberately separate from this class so the actual checking logic stays testable without
/// exercising a real timer loop.
/// </summary>
internal sealed class IntegrationHealthChecker(
    IEnumerable<IIntegrationConnector> connectors,
    IIntegrationHealthMonitor monitor,
    IClock clock,
    ILogger<IntegrationHealthChecker> logger) : IIntegrationHealthChecker
{
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        foreach (var connector in connectors)
        {
            try
            {
                var status = await connector.GetStatusAsync(probe: true, cancellationToken).ConfigureAwait(false);
                monitor.Record(status.Key, status.Status, status.Message, clock.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One connector failing to even report its own status (a bug, an unexpected
                // exception from a probe) must not stop the others from being checked.
                logger.LogWarning(ex, "Health check failed for integration connector {Key}", connector.Key);
                monitor.Record(connector.Key, "Unreachable", ex.Message, clock.UtcNow);
            }
        }
    }
}
