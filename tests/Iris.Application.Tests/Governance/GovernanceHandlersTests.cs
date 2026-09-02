using Iris.Application.Governance;
using Iris.Application.Tests.Fakes;
using Iris.Application.Common;
using Iris.Domain.Access;
using Iris.Domain.Tenancy;

namespace Iris.Application.Tests.Governance;

public sealed class GovernanceHandlersTests
{
    private static FakeStore StoreWithReaderRole(out Role reader)
    {
        reader = new Role(Guid.NewGuid(), "reader", "Reader", isBuiltIn: true);
        reader.Grant(PermissionId.Parse(Permissions.Overview.Read));
        return new FakeStore().WithRole(reader);
    }

    [Fact]
    public async Task CreateCustomer_persists_and_rejects_duplicate_key()
    {
        var store = new FakeStore();
        var handler = new CreateCustomerHandler(store.CustomerRepository, store.UnitOfWork);

        var created = await handler.HandleAsync(new CreateCustomerCommand("ACME", "Acme Ltd"));

        Assert.Equal("acme", created.Key);
        Assert.Equal(1, store.SaveChangesCalls);
        Assert.Single(store.Customers);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateCustomerCommand("acme", "Acme again")));
    }

    [Fact]
    public async Task AddContext_requires_an_existing_customer_and_unique_name()
    {
        var store = new FakeStore();
        var handler = new AddContextHandler(store.CustomerRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AddContextCommand(Guid.NewGuid(), "Prod", "Production")));

        var customer = new Customer(Guid.NewGuid(), "acme", "Acme");
        store.WithCustomer(customer);

        var ctx = await handler.HandleAsync(new AddContextCommand(customer.Id, "Prod", "production"));
        Assert.Equal("Production", ctx.Kind);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new AddContextCommand(customer.Id, "prod", "Test")));
    }

    [Fact]
    public async Task AddContext_rejects_unknown_kind()
    {
        var store = new FakeStore();
        var customer = new Customer(Guid.NewGuid(), "acme", "Acme");
        store.WithCustomer(customer);
        var handler = new AddContextHandler(store.CustomerRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new AddContextCommand(customer.Id, "Weird", "QA")));
    }

    [Fact]
    public async Task UpdateCustomer_renames_and_toggles_active()
    {
        var store = new FakeStore();
        var customer = new Customer(Guid.NewGuid(), "acme", "Acme");
        store.WithCustomer(customer);

        var handler = new UpdateCustomerHandler(store.CustomerRepository, store.UnitOfWork);

        var updated = await handler.HandleAsync(new UpdateCustomerCommand(customer.Id, "Acme Ltd", false));

        Assert.Equal("Acme Ltd", updated.Name);
        Assert.Equal("acme", updated.Key); // unchanged — Key is immutable
        Assert.False(updated.IsActive);

        var reactivated = await handler.HandleAsync(new UpdateCustomerCommand(customer.Id, "Acme Ltd", true));
        Assert.True(reactivated.IsActive);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new UpdateCustomerCommand(Guid.NewGuid(), "X", true)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new UpdateCustomerCommand(customer.Id, "", true)));
    }

    [Fact]
    public async Task AssignRole_grants_once_and_rejects_duplicates()
    {
        var store = StoreWithReaderRole(out var reader);
        var user = new User(Guid.NewGuid(), "ext-9", "u@iris.local", "U");
        store.WithUser(user);
        var customerId = Guid.NewGuid();

        var handler = new AssignRoleHandler(
            store.UserRepository, store.RoleRepository, store.RoleAssignmentRepository, store.UnitOfWork);

        var result = await handler.HandleAsync(
            new AssignRoleCommand(user.Id, "reader", "Customer", customerId, null));

        Assert.Equal("reader", result.RoleKey);
        Assert.Equal("Customer", result.ScopeType);
        Assert.Single(store.Assignments);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new AssignRoleCommand(user.Id, "reader", "Customer", customerId, null)));
    }

    [Fact]
    public async Task AssignRole_validates_user_role_and_scope()
    {
        var store = StoreWithReaderRole(out _);
        var handler = new AssignRoleHandler(
            store.UserRepository, store.RoleRepository, store.RoleAssignmentRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AssignRoleCommand(Guid.NewGuid(), "reader", "Global", null, null)));

        var user = new User(Guid.NewGuid(), "ext-x", "x@iris.local", "X");
        store.WithUser(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AssignRoleCommand(user.Id, "ghost", "Global", null, null)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new AssignRoleCommand(user.Id, "reader", "Customer", null, null)));
    }

    [Fact]
    public async Task CreateUser_pre_provisions_and_rejects_duplicate_email()
    {
        var store = new FakeStore();
        var handler = new CreateUserHandler(store.UserRepository, store.UnitOfWork);

        var created = await handler.HandleAsync(new CreateUserCommand("new.admin@customer.example", "New Admin"));

        Assert.False(created.IsProvisioned);
        Assert.Equal("new.admin@customer.example", created.Email);
        Assert.Empty(created.Assignments);
        Assert.Single(store.Users);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateUserCommand("new.admin@customer.example", "Someone else")));
    }

    [Fact]
    public async Task CreateUser_requires_email_and_display_name()
    {
        var store = new FakeStore();
        var handler = new CreateUserHandler(store.UserRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new CreateUserCommand("", "Someone")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new CreateUserCommand("someone@iris.local", "")));
    }

    [Fact]
    public async Task RevokeRole_removes_only_the_users_own_assignment()
    {
        var store = StoreWithReaderRole(out var reader);
        var user = new User(Guid.NewGuid(), "ext-r", "r@iris.local", "R");
        var assignment = new RoleAssignment(Guid.NewGuid(), user.Id, reader.Id, AccessScope.Global());
        store.WithUser(user).WithAssignment(assignment);

        var handler = new RevokeRoleHandler(store.RoleAssignmentRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new RevokeRoleCommand(Guid.NewGuid(), assignment.Id)));

        await handler.HandleAsync(new RevokeRoleCommand(user.Id, assignment.Id));
        Assert.Empty(store.Assignments);
    }

    [Fact]
    public async Task UpdateUser_edits_profile_and_active_flag()
    {
        var store = StoreWithReaderRole(out var reader);
        var user = new User(Guid.NewGuid(), "ext-u", "old@iris.local", "Old Name");
        store.WithUser(user).WithAssignment(new RoleAssignment(Guid.NewGuid(), user.Id, reader.Id, AccessScope.Global()));

        var handler = new UpdateUserHandler(
            store.UserRepository, store.RoleAssignmentRepository, store.RoleRepository, store.UnitOfWork);

        var updated = await handler.HandleAsync(new UpdateUserCommand(user.Id, "new@iris.local", "New Name", false));

        Assert.Equal("new@iris.local", updated.Email);
        Assert.Equal("New Name", updated.DisplayName);
        Assert.False(updated.IsActive);
        Assert.Single(updated.Assignments);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new UpdateUserCommand(Guid.NewGuid(), "x@iris.local", "X", true)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new UpdateUserCommand(user.Id, "", "X", true)));
    }

    [Fact]
    public async Task UpdateUser_rejects_an_email_another_user_already_has()
    {
        var store = new FakeStore();
        var a = new User(Guid.NewGuid(), "ext-a", "a@iris.local", "A");
        var b = new User(Guid.NewGuid(), "ext-b", "b@iris.local", "B");
        store.WithUser(a).WithUser(b);

        var handler = new UpdateUserHandler(
            store.UserRepository, store.RoleAssignmentRepository, store.RoleRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new UpdateUserCommand(b.Id, "a@iris.local", "B", true)));

        // keeping its own email is fine
        var ok = await handler.HandleAsync(new UpdateUserCommand(b.Id, "b@iris.local", "B renamed", true));
        Assert.Equal("B renamed", ok.DisplayName);
    }

    [Fact]
    public async Task DeleteUser_removes_the_user_and_their_assignments()
    {
        var store = StoreWithReaderRole(out var reader);
        var user = new User(Guid.NewGuid(), "ext-d", "d@iris.local", "D");
        store.WithUser(user).WithAssignment(new RoleAssignment(Guid.NewGuid(), user.Id, reader.Id, AccessScope.Global()));

        var handler = new DeleteUserHandler(store.UserRepository, store.UnitOfWork);

        await handler.HandleAsync(new DeleteUserCommand(user.Id));

        Assert.Empty(store.Users);
        Assert.Empty(store.Assignments);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new DeleteUserCommand(user.Id)));
    }
}
