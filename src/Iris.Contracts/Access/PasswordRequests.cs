namespace Iris.Contracts.Access;

/// <summary>
/// Body of <c>POST /auth/password</c>. <see cref="CurrentPassword"/> is required only when the
/// caller already has a local password (a change); it is ignored on the first set.
/// </summary>
public sealed record SetPasswordRequest(string NewPassword, string? CurrentPassword = null);
