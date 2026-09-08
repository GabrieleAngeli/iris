using Iris.Domain.Secrets;

namespace Iris.Application.Abstractions;

/// <summary>Persistence for <see cref="EncryptedSecretEntry"/> rows — one per fallback-store
/// secret reference, not a singleton. Backs <c>IFallbackSecretVault</c>.</summary>
public interface IFallbackSecretEntryRepository
{
    Task<EncryptedSecretEntry?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every row this owner has. The expected scale here is a handful of secrets at
    /// most (OpenBao/AWX/Ansible tokens, SMTP password, occasional ServerCredential) — loading
    /// full rows (ciphertext included) to compute status/restore counts is deliberately not
    /// optimized away with a separate lightweight/count-only query.</summary>
    Task<IReadOnlyList<EncryptedSecretEntry>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task AddAsync(EncryptedSecretEntry entry, CancellationToken cancellationToken = default);
}
