using Iris.Domain.Access;

namespace Iris.Domain.Tests.Access;

public sealed class PermissionResolverTests
{
    private static readonly Guid CustomerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ContextA1 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public void Unions_permissions_from_every_covering_grant()
    {
        EffectiveGrant[] grants =
        [
            new(AccessScope.Global(), [Permissions.Overview.Read]),
            new(AccessScope.ForCustomer(CustomerA), [Permissions.Infrastructure.Read, Permissions.Deployments.Read]),
            new(AccessScope.ForContext(CustomerA, ContextA1), [Permissions.Deployments.Prepare]),
        ];

        var effective = PermissionResolver.Resolve(grants, AccessScope.ForContext(CustomerA, ContextA1));

        Assert.Equal(
            new[]
            {
                Permissions.Overview.Read,
                Permissions.Infrastructure.Read,
                Permissions.Deployments.Read,
                Permissions.Deployments.Prepare,
            }.OrderBy(x => x),
            effective.OrderBy(x => x));
    }

    [Fact]
    public void Ignores_grants_whose_scope_does_not_cover_the_target()
    {
        EffectiveGrant[] grants =
        [
            new(AccessScope.ForContext(CustomerA, ContextA1), [Permissions.Deployments.Prepare]),
        ];

        var effective = PermissionResolver.Resolve(grants, AccessScope.ForCustomer(CustomerA));

        Assert.Empty(effective);
    }

    [Fact]
    public void Platform_admin_expands_to_the_full_catalog()
    {
        EffectiveGrant[] grants = [new(AccessScope.Global(), [Permissions.PlatformAdmin])];

        var effective = PermissionResolver.Resolve(grants, AccessScope.ForContext(CustomerA, ContextA1));

        // effective must contain every catalog permission
        Assert.Superset(Permissions.All.ToHashSet(), effective.ToHashSet());
    }

    [Fact]
    public void IsAllowed_honours_scope_and_platform_admin()
    {
        EffectiveGrant[] adminGrant = [new(AccessScope.Global(), [Permissions.PlatformAdmin])];
        EffectiveGrant[] scopedGrant = [new(AccessScope.ForCustomer(CustomerA), [Permissions.Infrastructure.Read])];

        Assert.True(PermissionResolver.IsAllowed(
            adminGrant, PermissionId.Parse(Permissions.Governance.ManageRoles), AccessScope.ForCustomer(CustomerA)));

        Assert.True(PermissionResolver.IsAllowed(
            scopedGrant, PermissionId.Parse(Permissions.Infrastructure.Read), AccessScope.ForContext(CustomerA, ContextA1)));

        Assert.False(PermissionResolver.IsAllowed(
            scopedGrant, PermissionId.Parse(Permissions.Infrastructure.Write), AccessScope.ForCustomer(CustomerA)));
    }
}
