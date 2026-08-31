using Iris.Domain.Access;

namespace Iris.Domain.Tests.Access;

public sealed class RoleTests
{
    private static Role NewRole() => new(Guid.NewGuid(), "operator", "Operator");

    [Fact]
    public void Grant_is_idempotent()
    {
        var role = NewRole();
        var permission = PermissionId.Parse(Permissions.Deployments.Prepare);

        Assert.True(role.Grant(permission));
        Assert.False(role.Grant(permission));
        Assert.Single(role.Permissions);
        Assert.True(role.HasPermission(permission));
    }

    [Fact]
    public void Revoke_removes_a_granted_permission()
    {
        var role = NewRole();
        var permission = PermissionId.Parse(Permissions.Actions.Run);
        role.Grant(permission);

        Assert.True(role.Revoke(permission));
        Assert.False(role.HasPermission(permission));
        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void ReplacePermissions_deduplicates_and_overwrites()
    {
        var role = NewRole();
        role.Grant(PermissionId.Parse(Permissions.Overview.Read));

        role.ReplacePermissions(
        [
            PermissionId.Parse(Permissions.Infrastructure.Read),
            PermissionId.Parse(Permissions.Infrastructure.Read),
            PermissionId.Parse(Permissions.Applications.Read),
        ]);

        Assert.Equal(2, role.Permissions.Count);
        Assert.Contains(Permissions.Infrastructure.Read, role.Permissions);
        Assert.DoesNotContain(Permissions.Overview.Read, role.Permissions);
    }

    [Fact]
    public void Key_is_normalised_to_lowercase()
    {
        var role = new Role(Guid.NewGuid(), "Platform-Admin", "Platform Admin", isBuiltIn: true);

        Assert.Equal("platform-admin", role.Key);
        Assert.True(role.IsBuiltIn);
    }
}
