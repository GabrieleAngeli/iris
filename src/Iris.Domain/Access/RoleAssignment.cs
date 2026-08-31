using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// Binds a <see cref="Role"/> to a <see cref="User"/> at a specific
/// <see cref="AccessScope"/>. This is the unit of "capillary" authorization:
/// the same user can hold different roles on different customers and contexts.
/// </summary>
public sealed class RoleAssignment : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private RoleAssignment()
        : base(Guid.Empty)
    {
        Scope = AccessScope.Global();
    }

    public RoleAssignment(Guid id, Guid userId, Guid roleId, AccessScope scope)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role id is required.", nameof(roleId));
        }

        UserId = userId;
        RoleId = roleId;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public AccessScope Scope { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
