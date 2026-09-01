using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// A person who can sign in to Iris. Identity is federated: <see cref="ExternalId"/>
/// is the stable subject/object id issued by the identity provider (Entra ID
/// <c>oid</c>, or a synthetic id for local development).
/// </summary>
public sealed class User : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private User()
        : base(Guid.Empty)
    {
        ExternalId = string.Empty;
        Email = string.Empty;
        DisplayName = string.Empty;
    }

    public User(Guid id, string externalId, string email, string displayName)
        : base(id)
    {
        ExternalId = Guard(externalId, nameof(externalId));
        Email = Guard(email, nameof(email));
        DisplayName = Guard(displayName, nameof(displayName));
        IsActive = true;
        IsProvisioned = true;
    }

    public string ExternalId { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// False for a user an admin created ahead of their first sign-in (see
    /// <see cref="Invite"/>) — <see cref="ExternalId"/> is a synthetic placeholder until
    /// <see cref="ClaimIdentity"/> links the account to their real identity provider subject.
    /// </summary>
    public bool IsProvisioned { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Pre-provisions a user an admin wants to grant access to before they've ever signed
    /// in. <see cref="ExternalId"/> is a synthetic placeholder, replaced by
    /// <see cref="ClaimIdentity"/> the first time this person actually authenticates.
    /// </summary>
    public static User Invite(Guid id, string email, string displayName)
    {
        var user = new User(id, $"pending:{Guid.NewGuid():N}", email, displayName)
        {
            IsProvisioned = false,
        };
        return user;
    }

    /// <summary>Refresh the mutable profile fields from the identity provider on sign-in.</summary>
    public void SyncProfile(string email, string displayName)
    {
        Email = Guard(email, nameof(email));
        DisplayName = Guard(displayName, nameof(displayName));
    }

    /// <summary>
    /// Links a pre-provisioned (<see cref="IsProvisioned"/> false) user to the real identity
    /// from their first sign-in. Called at most once per user.
    /// </summary>
    public void ClaimIdentity(string externalId, string email, string displayName)
    {
        ExternalId = Guard(externalId, nameof(externalId));
        Email = Guard(email, nameof(email));
        DisplayName = Guard(displayName, nameof(displayName));
        IsProvisioned = true;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Guard(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }
}
