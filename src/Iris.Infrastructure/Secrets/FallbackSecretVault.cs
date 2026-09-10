using System.Security.Cryptography;
using Iris.Application.Abstractions;
using Iris.Domain.Secrets;
using Iris.Infrastructure.Security;

namespace Iris.Infrastructure.Secrets;

/// <summary>
/// Bridges <see cref="EncryptedFallbackSecretStore"/>'s in-memory cache and its encrypted, durable
/// copy in Iris's own database (<see cref="EncryptedSecretEntry"/>). Always registered; once
/// OpenBao is the live store (<c>SwitchableSecretStore</c>) the cache is empty, so this reports
/// nothing to unlock and <see cref="UnlockAsync"/> is a no-op.
///
/// Deliberately never keeps a password around beyond one call: the caller
/// (<c>UnlockFallbackSecretsHandler</c>) has already verified it against the admin's stored hash
/// before this runs; this class just uses it once, synchronously, as key-derivation input.
/// </summary>
internal sealed class FallbackSecretVault(
    IFallbackSecretCache cache,
    IFallbackSecretEntryRepository entries,
    AesGcmSecretProtector protector,
    IClock clock,
    IUnitOfWork unitOfWork) : IFallbackSecretVault
{
    public async Task<FallbackSecretVaultStatus> GetStatusAsync(Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        if (currentUserId is not { } userId)
        {
            return FallbackSecretVaultStatus.None;
        }

        var inMemoryReferences = cache.Snapshot().Keys.ToHashSet(StringComparer.Ordinal);
        var ownedRows = await entries.GetByOwnerAsync(userId, cancellationToken).ConfigureAwait(false);
        var ownedReferences = ownedRows.Select(row => row.Reference).ToHashSet(StringComparer.Ordinal);

        // "Pending persist": in memory but not yet a durable row owned by this caller — includes
        // both brand-new secrets and ones currently owned by a different admin (unlocking would
        // reassign them, see UnlockAsync's remarks).
        var pendingPersistCount = inMemoryReferences.Count(reference => !ownedReferences.Contains(reference));
        var restorableCount = ownedReferences.Count(reference => !inMemoryReferences.Contains(reference));

        return new FallbackSecretVaultStatus(
            pendingPersistCount > 0 || restorableCount > 0,
            pendingPersistCount,
            restorableCount);
    }

    public async Task<FallbackSecretUnlockResult> UnlockAsync(
        Guid userId,
        string verifiedPassword,
        CancellationToken cancellationToken = default)
    {
        var persisted = 0;
        var ownershipTransferred = 0;
        var now = clock.UtcNow;

        // Persist first: every secret currently only in memory (this process's own writes since
        // it last started, or since the last Unlock) becomes durable, encrypted under the
        // caller's password. A row that already exists under a *different* owner has its
        // ownership reassigned to the caller — safe (gated behind platform.admin, never leaks the
        // previous owner's plaintext, just re-encrypts what's currently live in RAM) and reported
        // honestly via OwnershipTransferred rather than hidden.
        foreach (var (reference, plaintext) in cache.Snapshot())
        {
            var protectedValue = protector.Protect(plaintext, verifiedPassword, reference);
            var existing = await entries.GetByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                await entries.AddAsync(EncryptedSecretEntry.Create(reference, userId, protectedValue), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (existing.OwnerUserId != userId)
                {
                    ownershipTransferred++;
                }

                existing.Overwrite(userId, protectedValue, now);
            }

            persisted++;
        }

        // Then restore: every durable row owned by this same caller that isn't already in memory
        // (i.e. from a previous process run) comes back. A row that fails to decrypt (corrupted
        // ciphertext) is skipped, not thrown — one bad row must not block the rest.
        var restored = 0;
        var ownedRows = await entries.GetByOwnerAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var row in ownedRows)
        {
            if (cache.Contains(row.Reference))
            {
                continue;
            }

            try
            {
                var plaintext = protector.Unprotect(row.ProtectedValue, verifiedPassword, row.Reference);
                cache.Populate(row.Reference, plaintext);
                restored++;
            }
            catch (CryptographicException)
            {
                // Corrupted row — leave it locked rather than fail the whole unlock.
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new FallbackSecretUnlockResult(persisted, restored, ownershipTransferred);
    }
}
