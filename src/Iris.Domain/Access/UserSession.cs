using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// A signed-in session created by <c>POST /auth/login</c> — the local-password counterpart to an
/// SSO-issued bearer token. Only the SHA-256 <see cref="TokenHash"/> of the raw token is stored —
/// the token itself is handed to the caller once, at login time, and never again. Fixed lifetime,
/// no sliding expiration: once it lapses, sign in again.
/// </summary>
public sealed class UserSession : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private UserSession()
        : base(Guid.Empty)
    {
        TokenHash = string.Empty;
    }

    private UserSession(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Session must belong to a real user.", nameof(userId));
        }

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    /// <summary>Hex-encoded SHA-256 of the raw token. The raw token is never persisted.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static UserSession Issue(Guid id, Guid userId, string tokenHash, DateTimeOffset nowUtc, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Session lifetime must be positive.");
        }

        return new UserSession(id, userId, tokenHash, nowUtc.Add(lifetime));
    }

    public bool IsValid(DateTimeOffset nowUtc) => ExpiresAtUtc > nowUtc;
}
