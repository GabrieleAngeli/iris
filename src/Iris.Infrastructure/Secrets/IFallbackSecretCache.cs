namespace Iris.Infrastructure.Secrets;

/// <summary>Infrastructure-only view onto <see cref="EncryptedFallbackSecretStore"/>'s in-memory
/// dictionary — deliberately not an Application-layer port, since only <see cref="FallbackSecretVault"/>
/// needs to reach into it (every other caller only ever sees the plain <c>ISecretStore</c>
/// contract).</summary>
internal interface IFallbackSecretCache
{
    /// <summary>Every reference/plaintext pair currently held in memory.</summary>
    IReadOnlyDictionary<string, string> Snapshot();

    bool Contains(string reference);

    /// <summary>Populates (or overwrites) one entry — used when a durable row is decrypted back
    /// into memory during Unlock.</summary>
    void Populate(string reference, string plaintext);
}
