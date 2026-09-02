namespace Iris.Contracts.Access;

public sealed record RequestPasswordResetRequest(string Email);

public sealed record RequestPasswordResetResponse(bool Sent);

