using System.Collections.Concurrent;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Secrets;

/// <summary>
/// Fallback <see cref="ISecretStore"/> used whenever OpenBao itself isn't the active store (before
/// it's configured, or if it's unreachable). Exactly like the <c>InMemorySecretStore</c> it
/// replaces: a process-local dictionary is the one and only thing every <c>ISecretStore</c> caller
/// ever sees — <see cref="StoreAsync"/>/<see cref="RetrieveAsync"/>/<see cref="DeleteAsync"/> have
/// zero DB code and zero behavior change from before.
///
/// What's new lives entirely in <see cref="FallbackSecretVault"/>, which reaches into this same
/// dictionary through <see cref="IFallbackSecretCache"/> to make entries durable (encrypted with a
/// live admin's password) and to restore previously-durable ones back into it — see that class and
/// <c>EncryptedSecretEntry</c> for the full design. This split exists because <c>ISecretStore</c>
/// is registered as a singleton (the dictionary must outlive any one request), while DB access
/// needs an ordinary scoped <c>IrisDbContext</c> — a singleton can't own one cleanly.
/// </summary>
internal sealed class EncryptedFallbackSecretStore : ISecretStore, IFallbackSecretCache
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default)
    {
        var reference = $"mock-openbao:{logicalPath}";
        _values[reference] = secretValue;
        return Task.FromResult(reference);
    }

    public Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.GetValueOrDefault(reference));

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        // Dictionary-only, deliberately: a durable encrypted row (if this reference was ever
        // unlocked once) is left behind as orphaned ciphertext. Harmless — nothing references it
        // — and not solved here, see FallbackSecretVault's remarks. Wiring DB deletes into this
        // singleton would need the same scoped-DbContext-from-a-singleton problem this whole split
        // exists to avoid, for a cosmetic cleanup concern.
        _values.TryRemove(reference, out _);
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, string> Snapshot() => _values;

    public bool Contains(string reference) => _values.ContainsKey(reference);

    public void Populate(string reference, string plaintext) => _values[reference] = plaintext;
}
