using System.Security.Cryptography;
using System.Text;

namespace Iris.Infrastructure.Security;

/// <summary>
/// Encrypts a single secret value with a key derived from a live, plaintext password — never the
/// password itself, never a derived key, is ever persisted. Same packed-string convention as
/// <see cref="Pbkdf2PasswordHasher"/> (<c>prefix$iterations$...</c>, so the work factor travels
/// with the value), but this is a distinct KDF call with its own salt — deliberately never reuses
/// a stored login password *hash* as key material, which would mix an authentication artifact
/// with an encryption key.
///
/// <paramref name="associatedData"/> on both <see cref="Protect"/>/<see cref="Unprotect"/> should
/// be the secret's own <c>ISecretStore</c> reference string, passed as AES-GCM associated data
/// (AAD): this binds each ciphertext to the specific row it belongs to, so a ciphertext blob
/// swapped between two rows (bad migration, restore-from-backup mistake, manual SQL) fails the
/// GCM tag check immediately instead of silently decrypting under the wrong row.
///
/// First symmetric-encryption code in this repository — see
/// <c>EncryptedSecretEntry</c>/<c>FallbackSecretVault</c> for what uses it and why.
/// </summary>
internal sealed class AesGcmSecretProtector
{
    // Matches Pbkdf2PasswordHasher's iteration count — same cost factor, chosen independently
    // for this purpose, not a shared constant (the two KDF calls must stay decoupled).
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32; // AES-256
    private const int NonceSize = 12; // AesGcm.NonceByteSizes: 96-bit is the recommended size
    private const int TagSize = 16;
    private const string Prefix = "aesgcm-pbkdf2sha256";

    public string Protect(string plaintext, string password, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(associatedData);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt);
        var aad = Encoding.UTF8.GetBytes(associatedData);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);
        }

        return string.Join('$',
            Prefix,
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    /// <summary>Throws <see cref="CryptographicException"/> on a wrong password, tampered
    /// ciphertext, or mismatched <paramref name="associatedData"/> — all three surface
    /// identically, which is correct: none of them should be distinguishable to a caller.</summary>
    public string Unprotect(string protectedValue, string password, string associatedData)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedValue);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(associatedData);

        var parts = protectedValue.Split('$');
        if (parts.Length != 6 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
        {
            throw new CryptographicException("Unrecognized protected value format.");
        }

        byte[] salt, nonce, tag, ciphertext;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            nonce = Convert.FromBase64String(parts[3]);
            tag = Convert.FromBase64String(parts[4]);
            ciphertext = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Malformed protected value.", ex);
        }

        var key = DeriveKey(password, salt, iterations);
        var aad = Encoding.UTF8.GetBytes(associatedData);
        var plaintextBytes = new byte[ciphertext.Length];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, aad);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations = Iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
