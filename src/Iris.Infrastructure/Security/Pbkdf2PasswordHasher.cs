using System.Security.Cryptography;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Security;

/// <summary>
/// PBKDF2 (SHA-256) password hasher. Encoded as <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;</c>
/// with base64 salt and derived key, so the work factor travels with the hash.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Prefix = "pbkdf2-sha256";

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
