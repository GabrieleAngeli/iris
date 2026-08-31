using Iris.Application.Access;
using Iris.Application.Tenancy;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;
using Iris.Domain.Tenancy;

namespace Iris.Application.Tests.Access;

public sealed class AccessServicesTests
{
    private static readonly Guid ContosoId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid GlobexId = Guid.Parse("c0000000-0000-0000-0000-000000000002");

    private static (FakeStore Store, Role Operator, Role Admin) SeedRoles()
    {
        var op = new Role(Guid.NewGuid(), "operator", "Operator");
        op.ReplacePermissions([
            PermissionId.Parse(Permissions.Infrastructure.Read),
            PermissionId.Parse(Permissions.Deployments.Prepare),
        ]);

        var admin = new Role(Guid.NewGuid(), "platform-admin", "Platform Administrator", isBuiltIn: true);
        admin.Grant(PermissionId.Parse(Permissions.PlatformAdmin));

        var store = new FakeStore().WithRole(op).WithRole(admin);
        return (store, op, admin);
    }

    private static UserAccessService AccessService(FakeStore store) =>
        new(store.UserRepository, store.RoleAssignmentRepository, store.RoleRepository);

    [Fact]
    public async Task Snapshot_flattens_assignments_with_role_permissions()
    {
        var (store, op, _) = SeedRoles();
        var user = new User(Guid.NewGuid(), "ext-1", "op@iris.local", "Op");
        store.WithUser(user)
            .WithAssignment(new RoleAssignment(Guid.NewGuid(), user.Id, op.Id, AccessScope.ForCustomer(ContosoId)));

        var snapshot = await AccessService(store).GetSnapshotAsync("ext-1");

        Assert.NotNull(snapshot);
        var assignment = Assert.Single(snapshot!.Assignments);
        Assert.Equal("operator", assignment.RoleKey);
        Assert.Contains(Permissions.Infrastructure.Read, assignment.Permissions);
        Assert.Equal(ContosoId, assignment.Scope.CustomerId);
    }

    [Fact]
    public async Task PermissionAuthorizer_respects_scope()
    {
        var (store, op, _) = SeedRoles();
        var user = new User(Guid.NewGuid(), "ext-2", "op@iris.local", "Op");
        store.WithUser(user)
            .WithAssignment(new RoleAssignment(Guid.NewGuid(), user.Id, op.Id, AccessScope.ForCustomer(ContosoId)));

        var authorizer = new PermissionAuthorizer(AccessService(store));

        Assert.True(await authorizer.IsAllowedAsync(
            "ext-2", PermissionId.Parse(Permissions.Infrastructure.Read), AccessScope.ForCustomer(ContosoId)));
        Assert.False(await authorizer.IsAllowedAsync(
            "ext-2", PermissionId.Parse(Permissions.Infrastructure.Read), AccessScope.ForCustomer(GlobexId)));
        Assert.False(await authorizer.IsAllowedAsync(
            "ext-2", PermissionId.Parse(Permissions.Infrastructure.Write), AccessScope.ForCustomer(ContosoId)));
    }

    [Fact]
    public async Task PermissionAuthorizer_denies_unknown_user()
    {
        var (store, _, _) = SeedRoles();
        var authorizer = new PermissionAuthorizer(AccessService(store));

        Assert.False(await authorizer.IsAllowedAsync(
            "nobody", PermissionId.Parse(Permissions.Overview.Read), AccessScope.Global()));
    }

    [Fact]
    public async Task Provisioning_creates_user_on_first_sign_in_then_reuses_it()
    {
        var (store, _, _) = SeedRoles();
        var provisioning = new UserProvisioningService(store.UserRepository, store.UnitOfWork);
        var principal = new StubCurrentUser("ext-new", "new@iris.local", "New User");

        var first = await provisioning.EnsureProvisionedAsync(principal);
        var second = await provisioning.EnsureProvisionedAsync(principal);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("new@iris.local", first.Email);
        Assert.Equal(1, store.SaveChangesCalls);
    }

    [Fact]
    public async Task ListAccessibleCustomers_returns_only_visible_customers_and_contexts()
    {
        var (store, op, _) = SeedRoles();

        var contoso = new Customer(ContosoId, "contoso", "Contoso");
        var contosoProd = contoso.AddContext(Guid.NewGuid(), "Production", ContextKind.Production);
        contoso.AddContext(Guid.NewGuid(), "Test", ContextKind.Test);
        var globex = new Customer(GlobexId, "globex", "Globex");
        globex.AddContext(Guid.NewGuid(), "Production", ContextKind.Production);
        store.WithCustomer(contoso).WithCustomer(globex);

        var user = new User(Guid.NewGuid(), "ext-3", "op@iris.local", "Op");
        store.WithUser(user)
            .WithAssignment(new RoleAssignment(
                Guid.NewGuid(), user.Id, op.Id, AccessScope.ForContext(ContosoId, contosoProd.Id)));

        var handler = new ListAccessibleCustomersHandler(
            new StubCurrentUser("ext-3", "op@iris.local", "Op"),
            new UserProvisioningService(store.UserRepository, store.UnitOfWork),
            AccessService(store),
            store.CustomerRepository);

        var result = await handler.HandleAsync(new ListAccessibleCustomersQuery());

        var only = Assert.Single(result);
        Assert.Equal("contoso", only.Key);
        var ctx = Assert.Single(only.Contexts);
        Assert.Equal("Production", ctx.Name);
    }
}
