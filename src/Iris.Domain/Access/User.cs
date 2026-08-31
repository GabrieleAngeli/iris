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
    }

    public string ExternalId { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Refresh the mutable profile fields from the identity provider on sign-in.</summary>
    public void SyncProfile(string email, string displayName)
    {
        Email = Guard(email, nameof(email));
        DisplayName = Guard(displayName, nameof(displayName));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Guard(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }
}
