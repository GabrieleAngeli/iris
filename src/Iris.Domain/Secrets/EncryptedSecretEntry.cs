using Iris.Domain.Common;

namespace Iris.Domain.Secrets;

/// <summary>
/// One secret, encrypted at rest, belonging to the fallback <c>ISecretStore</c> used before
/// OpenBao is configured (see <c>Iris.Infrastructure/Secrets/EncryptedFallbackSecretStore.cs</c>
/// and <c>FallbackSecretVault</c>). <see cref="ProtectedValue"/> is one packed
/// <c>aesgcm-pbkdf2sha256$...</c> string produced by <c>AesGcmSecretProtector</c> — the key is
/// derived from the plaintext password of the admin who saved it (<see cref="OwnerUserId"/>),
/// never stored anywhere. Only that admin can decrypt it again later, via an explicit "Unlock"
/// action — there is no way for Iris itself to decrypt this without a live human re-entering
/// their password.
///
/// One row per <see cref="Reference"/> (the same opaque string <c>ISecretStore</c> callers
/// already treat as their reference) — not a singleton like <c>IntegrationSettings</c>/
/// <c>MailProviderSettings</c>.
/// </summary>
public sealed class EncryptedSecretEntry : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private EncryptedSecretEntry()
        : base(Guid.Empty)
    {
        Reference = string.Empty;
        ProtectedValue = string.Empty;
    }

    private EncryptedSecretEntry(Guid id, string reference, Guid ownerUserId, string protectedValue)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        }

        Reference = reference;
        OwnerUserId = ownerUserId;
        ProtectedValue = protectedValue;
    }

    /// <summary>The <c>ISecretStore</c> reference this row backs — unique per row.</summary>
    public string Reference { get; private set; }

    /// <summary>Whose password the encryption key was derived from. Only this user's password
    /// can ever decrypt <see cref="ProtectedValue"/> again — never a navigation property, same
    /// plain-Guid convention as every other cross-aggregate reference in this codebase (e.g.
    /// <c>ApplicationInstallation.ServerNodeId</c>).</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Packed <c>aesgcm-pbkdf2sha256$iterations$salt$nonce$tag$ciphertext</c> string.
    /// No length cap — an encrypted SSH private key can be long.</summary>
    public string ProtectedValue { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static EncryptedSecretEntry Create(string reference, Guid ownerUserId, string protectedValue) =>
        new(Guid.CreateVersion7(), reference, ownerUserId, protectedValue);

    /// <summary>Re-encrypts this row in place — used both for an ordinary re-save by the same
    /// owner, and for an ownership transfer (a different admin persists a reference that already
    /// had a row from a previous owner; see <c>FallbackSecretVault</c> remarks on why that's safe
    /// and intentional).</summary>
    public void Overwrite(Guid ownerUserId, string protectedValue, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        }

        OwnerUserId = ownerUserId;
        ProtectedValue = protectedValue;
        UpdatedAtUtc = nowUtc;
    }
}
