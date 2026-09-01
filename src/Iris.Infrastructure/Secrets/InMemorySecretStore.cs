using System.Collections.Concurrent;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Secrets;

/// <summary>
/// Stand-in for OpenBao: holds secret values in a process-local dictionary, never in the
/// Iris database. Good enough for the demo/first-increment — swap for a real OpenBao-backed
/// <see cref="ISecretStore"/> when that integration lands; callers only see the abstraction.
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default)
    {
        var reference = $"mock-openbao:{logicalPath}";
        _values[reference] = secretValue;
        return Task.FromResult(reference);
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(reference, out _);
        return Task.CompletedTask;
    }
}
