using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// A short-lived advisory lock taken while one operator has an editor open on a resource
/// (a user, a server…), so a second operator sees "being edited by X" instead of silently
/// overwriting their work. Locks expire on their own (<see cref="ExpiresAtUtc"/>): the client
/// refreshes while its editor stays open and releases on close, but a crashed client just
/// lets the lock lapse.
/// </summary>
public sealed class EditLock : Entity<Guid>, IAggregateRoot
{
    // For the persistence layer.
    private EditLock()
        : base(Guid.Empty)
    {
        ResourceType = string.Empty;
        HolderDisplayName = string.Empty;
    }

    private EditLock(
        Guid id,
        string resourceType,
        Guid resourceId,
        Guid holderUserId,
        string holderDisplayName,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderDisplayName);

        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id is required.", nameof(resourceId));
        }

        if (holderUserId == Guid.Empty)
        {
            throw new ArgumentException("Holder user id is required.", nameof(holderUserId));
        }

        ResourceType = resourceType;
        ResourceId = resourceId;
        HolderUserId = holderUserId;
        HolderDisplayName = holderDisplayName;
        AcquiredAtUtc = acquiredAtUtc;
        RefreshedAtUtc = acquiredAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string ResourceType { get; private set; }

    public Guid ResourceId { get; private set; }

    public Guid HolderUserId { get; private set; }

    /// <summary>Denormalised so a waiting operator can be told who holds the lock without a second query.</summary>
    public string HolderDisplayName { get; private set; }

    public DateTimeOffset AcquiredAtUtc { get; private set; }

    public DateTimeOffset RefreshedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public static EditLock Acquire(
        Guid id,
        string resourceType,
        Guid resourceId,
        Guid holderUserId,
        string holderDisplayName,
        DateTimeOffset nowUtc,
        TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Lock TTL must be positive.");
        }

        return new EditLock(id, resourceType, resourceId, holderUserId, holderDisplayName, nowUtc, nowUtc.Add(ttl));
    }

    public bool IsHeldBy(Guid userId) => HolderUserId == userId;

    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;

    /// <summary>Push the expiry out — the heartbeat while an editor stays open.</summary>
    public void Refresh(DateTimeOffset nowUtc, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Lock TTL must be positive.");
        }

        RefreshedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.Add(ttl);
    }

    /// <summary>Hand the lock to a new holder once the previous one has lapsed.</summary>
    public void TakeOver(Guid holderUserId, string holderDisplayName, DateTimeOffset nowUtc, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holderDisplayName);

        if (holderUserId == Guid.Empty)
        {
            throw new ArgumentException("Holder user id is required.", nameof(holderUserId));
        }

        HolderUserId = holderUserId;
        HolderDisplayName = holderDisplayName;
        AcquiredAtUtc = nowUtc;
        Refresh(nowUtc, ttl);
    }
}
