using System.Collections.Concurrent;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

internal sealed class IntegrationHealthMonitor : IIntegrationHealthMonitor
{
    private readonly ConcurrentDictionary<string, IntegrationHealthSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public IntegrationHealthSnapshot? GetSnapshot(string key) => _snapshots.GetValueOrDefault(key);

    public void Record(string key, string status, string? message, DateTimeOffset checkedAtUtc) =>
        _snapshots[key] = new IntegrationHealthSnapshot(status, message, checkedAtUtc);
}
