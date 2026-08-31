using Iris.Domain.Common;

namespace Iris.Domain.Access;

/// <summary>
/// A named bundle of <see cref="PermissionId"/> values. Roles are scope-agnostic;
/// a <see cref="RoleAssignment"/> binds a role to a user at a given scope.
/// </summary>
public sealed class Role : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<string> _permissions = [];

    // For the persistence layer.
    private Role()
        : base(Guid.Empty)
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    public Role(Guid id, string key, string name, string? description = null, bool isBuiltIn = false)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Key = key.Trim().ToLowerInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        IsBuiltIn = isBuiltIn;
    }

    public string Key { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Built-in roles are seeded by Iris and cannot be deleted by operators.</summary>
    public bool IsBuiltIn { get; private set; }

    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool Grant(PermissionId permission)
    {
        if (_permissions.Contains(permission.Value))
        {
            return false;
        }

        _permissions.Add(permission.Value);
        return true;
    }

    public bool Revoke(PermissionId permission) => _permissions.Remove(permission.Value);

    public bool HasPermission(PermissionId permission) => _permissions.Contains(permission.Value);

    public void ReplacePermissions(IEnumerable<PermissionId> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions.Clear();
        foreach (var permission in permissions)
        {
            if (!_permissions.Contains(permission.Value))
            {
                _permissions.Add(permission.Value);
            }
        }
    }
}
