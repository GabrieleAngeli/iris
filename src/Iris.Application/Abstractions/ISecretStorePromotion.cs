namespace Iris.Application.Abstractions;

/// <summary>Raised by <see cref="ISecretStorePromotion.PromoteToOpenBaoAsync"/> when the target
/// OpenBao can't be verified — the live secret store is left unchanged.</summary>
public sealed class SecretStorePromotionException(string message) : Exception(message);

/// <summary>
/// Runtime switch of the active <c>ISecretStore</c> from the encrypted fallback vault to real
/// OpenBao, once its token is finally available (after an admin unlock). Migrates every
/// fallback-vault secret into OpenBao first, verifies OpenBao end to end, then flips the live
/// store — no Iris.Api restart. Implemented by <c>SwitchableSecretStore</c> in Iris.Infrastructure.
/// </summary>
public interface ISecretStorePromotion
{
    bool IsOpenBaoActive { get; }

    /// <summary>Verifies the target OpenBao (a KV write/read/delete round-trip), copies every
    /// in-memory fallback secret into it, then makes it the live store. Returns how many secrets
    /// were migrated. Throws <see cref="SecretStorePromotionException"/> without switching if the
    /// verification fails. A no-op returning 0 if OpenBao is already active.</summary>
    Task<int> PromoteToOpenBaoAsync(
        string endpoint,
        string token,
        string mountPath,
        bool useKvV2,
        CancellationToken cancellationToken = default);
}
