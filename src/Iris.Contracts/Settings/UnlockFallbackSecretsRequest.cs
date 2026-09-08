namespace Iris.Contracts.Settings;

/// <summary>Body of <c>POST /system/settings/secrets/unlock</c> — the caller's own login
/// password, re-entered live. Never persisted; used only to derive the encryption key for this
/// one request.</summary>
public sealed record UnlockFallbackSecretsRequest(string Password);

public sealed record UnlockFallbackSecretsResponse(
    int Persisted,
    int Restored,
    int OwnershipTransferred,
    string Message);
