using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// A one-time invitation issued for a <see cref="User"/> so an administrator can hand them a
/// link to bootstrap access before their first sign-in. Only the SHA-256 <see cref="TokenHash"/>
/// of the raw token is stored — the token itself is shown once, at issue time, and never again.
/// </summary>
public sealed class UserInvitation : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private UserInvitation()
        : base(Guid.Empty)
    {
        TokenHash = string.Empty;
    }

    private UserInvitation(Guid id, Guid userId, string tokenHash, Guid issuedByUserId, DateTimeOffset expiresAtUtc)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Invitation must belong to a real user.", nameof(userId));
        }

        UserId = userId;
        TokenHash = tokenHash;
        IssuedByUserId = issuedByUserId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    /// <summary>Hex-encoded SHA-256 of the raw token. The raw token is never persisted.</summary>
    public string TokenHash { get; private set; }

    public Guid IssuedByUserId { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Set the first time the token is redeemed; <c>null</c> while the invitation is still open.</summary>
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static UserInvitation Issue(
        Guid id,
        Guid userId,
        string tokenHash,
        Guid issuedByUserId,
        DateTimeOffset nowUtc,
        TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Invitation lifetime must be positive.");
        }

        return new UserInvitation(id, userId, tokenHash, issuedByUserId, nowUtc.Add(lifetime));
    }

    public bool IsPending(DateTimeOffset nowUtc) => ConsumedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Consume(DateTimeOffset nowUtc)
    {
        if (ConsumedAtUtc is not null)
        {
            throw new InvalidOperationException("This invitation has already been used.");
        }

        if (ExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException("This invitation has expired.");
        }

        ConsumedAtUtc = nowUtc;
    }
}
