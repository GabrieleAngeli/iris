using System.Security.Cryptography;
using Iris.Infrastructure.Security;

namespace Iris.Infrastructure.Tests.Security;

public sealed class AesGcmSecretProtectorTests
{
    [Fact]
    public void Protect_then_Unprotect_roundtrips()
    {
        var protector = new AesGcmSecretProtector();

        var protectedValue = protector.Protect("s.oi9YVUH2BqOJdjRl6BQKpOme", "correct-password", "mock-openbao:openbao/token");
        var plaintext = protector.Unprotect(protectedValue, "correct-password", "mock-openbao:openbao/token");

        Assert.Equal("s.oi9YVUH2BqOJdjRl6BQKpOme", plaintext);
    }

    [Fact]
    public void Unprotect_throws_with_the_wrong_password()
    {
        var protector = new AesGcmSecretProtector();
        var protectedValue = protector.Protect("secret-value", "correct-password", "ref");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(protectedValue, "wrong-password", "ref"));
    }

    [Fact]
    public void Unprotect_throws_when_the_ciphertext_is_tampered_with()
    {
        var protector = new AesGcmSecretProtector();
        var protectedValue = protector.Protect("secret-value", "password", "ref");

        var parts = protectedValue.Split('$');
        var ciphertextBytes = Convert.FromBase64String(parts[5]);
        ciphertextBytes[0] ^= 0xFF; // flip a bit
        parts[5] = Convert.ToBase64String(ciphertextBytes);
        var tampered = string.Join('$', parts);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tampered, "password", "ref"));
    }

    [Fact]
    public void Unprotect_throws_when_the_associated_data_does_not_match_what_was_used_to_protect()
    {
        // Proves the AAD binding actually works: a ciphertext blob "moved" to a different
        // reference (row) must not decrypt, even with the correct password.
        var protector = new AesGcmSecretProtector();
        var protectedValue = protector.Protect("secret-value", "password", "mock-openbao:openbao/token");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(protectedValue, "password", "mock-openbao:awx/token"));
    }

    [Fact]
    public void Protect_produces_different_output_for_the_same_input_every_time()
    {
        // Proves salt/nonce actually vary per call rather than being reused.
        var protector = new AesGcmSecretProtector();

        var first = protector.Protect("secret-value", "password", "ref");
        var second = protector.Protect("secret-value", "password", "ref");

        Assert.NotEqual(first, second);
    }
}
