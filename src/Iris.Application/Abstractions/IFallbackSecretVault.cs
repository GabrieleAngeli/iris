namespace Iris.Application.Abstractions;

/// <summary>Whether there's anything to unlock, for the current caller — surfaced in
/// <c>GET /system/settings</c> so the MAUI client knows whether to show an "Unlock" prompt.
/// Deliberately bare counts, no reference names/logical paths: which secrets are pending is not
/// leaked to anyone but the flow that actually unlocks them.</summary>
public sealed record FallbackSecretVaultStatus(bool HasPendingWork, int PendingPersistCount, int RestorableCount)
{
    public static readonly FallbackSecretVaultStatus None = new(false, 0, 0);
}

/// <summary><see cref="Persisted"/>: entries that were only in memory and are now durable.
/// <see cref="Restored"/>: entries loaded back from the database into memory.
/// <see cref="OwnershipTransferred"/>: a subset of <see cref="Persisted"/> that previously
/// belonged to a different admin and now belong to the caller — reported honestly rather than
/// hidden, see <c>FallbackSecretVault</c> remarks.</summary>
public sealed record FallbackSecretUnlockResult(int Persisted, int Restored, int OwnershipTransferred);

/// <summary>
/// Bridges the in-memory fallback <c>ISecretStore</c> cache and its encrypted, durable copy in
/// Iris's own database — see <c>EncryptedSecretEntry</c> and <c>FallbackSecretVault</c> for the
/// full design. A no-op once OpenBao is the live secret store (the cache is then empty).
/// </summary>
public interface IFallbackSecretVault
{
    Task<FallbackSecretVaultStatus> GetStatusAsync(Guid? currentUserId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="verifiedPassword"/> must already have been checked by the caller
    /// (see <c>UnlockFallbackSecretsHandler</c>) — this method trusts it and uses it directly as
    /// key-derivation input.</summary>
    Task<FallbackSecretUnlockResult> UnlockAsync(Guid userId, string verifiedPassword, CancellationToken cancellationToken = default);
}
