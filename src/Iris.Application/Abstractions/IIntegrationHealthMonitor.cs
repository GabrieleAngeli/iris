namespace Iris.Application.Abstractions;

/// <summary>One integration's last background-probed reachability — see
/// <c>IIntegrationHealthChecker</c> for what writes this and <c>GetSystemSettingsHandler</c> for
/// what reads it. Deliberately not persisted (a snapshot is only ever meaningful for as long as
/// this process has been running; it re-populates itself on the next probe cycle either way).</summary>
public sealed record IntegrationHealthSnapshot(string Status, string? Message, DateTimeOffset CheckedAtUtc);

/// <summary>Process-local cache of the last real (<c>probe: true</c>) status seen for each
/// integration connector. Read by <c>GetSystemSettingsHandler</c> so System settings/Dashboard
/// can show actual connectivity without making a live network call on every page load; written
/// periodically by <c>IIntegrationHealthChecker</c>.</summary>
public interface IIntegrationHealthMonitor
{
    IntegrationHealthSnapshot? GetSnapshot(string key);

    void Record(string key, string status, string? message, DateTimeOffset checkedAtUtc);
}

/// <summary>
/// Probes every registered <see cref="IIntegrationConnector"/> for real reachability and records
/// the result into <see cref="IIntegrationHealthMonitor"/> — the actual "does this service work"
/// check. <c>Iris.Api</c>'s background service just calls <see cref="RunOnceAsync"/> on a timer;
/// all the real logic (per-connector isolation, error handling) lives here so it's testable
/// without needing to exercise the timer loop itself.
/// </summary>
public interface IIntegrationHealthChecker
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
