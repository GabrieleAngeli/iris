namespace Iris.Contracts.Access;

/// <summary>Body of <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Result of <c>POST /auth/login</c>. <see cref="Token"/> is returned exactly once — send it as
/// <c>Authorization: Bearer &lt;token&gt;</c> on every subsequent request until it expires.
/// </summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAtUtc);
