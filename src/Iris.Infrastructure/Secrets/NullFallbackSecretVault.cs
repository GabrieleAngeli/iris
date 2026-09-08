using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Secrets;

/// <summary>Registered instead of <see cref="FallbackSecretVault"/> whenever OpenBao itself is the
/// active <c>ISecretStore</c> — there is nothing to unlock in that world (every secret already
/// goes through real OpenBao). <see cref="UnlockAsync"/> should never actually be reachable (the
/// endpoint/UI only offer it when <see cref="GetStatusAsync"/> reports pending work, which this
/// always reports as none) — it fails loudly rather than silently if it somehow is.</summary>
internal sealed class NullFallbackSecretVault : IFallbackSecretVault
{
    public Task<FallbackSecretVaultStatus> GetStatusAsync(Guid? currentUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FallbackSecretVaultStatus.None);

    public Task<FallbackSecretUnlockResult> UnlockAsync(Guid userId, string verifiedPassword, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("There is nothing to unlock — OpenBao is the active secret store.");
}
