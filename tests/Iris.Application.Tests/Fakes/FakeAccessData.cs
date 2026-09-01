using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Domain.Access;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Tests.Fakes;

/// <summary>Shared in-memory backing store for the fake repositories.</summary>
internal sealed class FakeStore
{
    public List<User> Users { get; } = [];

    public List<Role> Roles { get; } = [];

    public List<RoleAssignment> Assignments { get; } = [];

    public List<Customer> Customers { get; } = [];

    public List<ServerNode> Servers { get; } = [];

    public List<UserInvitation> Invitations { get; } = [];

    public List<EditLock> EditLocks { get; } = [];

    public Dictionary<string, string> SecretsByReference { get; } = [];

    public int SaveChangesCalls { get; set; }

    public FakeStore WithUser(User user)
    {
        Users.Add(user);
        return this;
    }

    public FakeStore WithRole(Role role)
    {
        Roles.Add(role);
        return this;
    }

    public FakeStore WithAssignment(RoleAssignment assignment)
    {
        Assignments.Add(assignment);
        return this;
    }

    public FakeStore WithCustomer(Customer customer)
    {
        Customers.Add(customer);
        return this;
    }

    public FakeStore WithServer(ServerNode server)
    {
        Servers.Add(server);
        return this;
    }

    public FakeUnitOfWork UnitOfWork => new(this);

    public FakeUserRepository UserRepository => new(this);

    public FakeRoleRepository RoleRepository => new(this);

    public FakeRoleAssignmentRepository RoleAssignmentRepository => new(this);

    public FakeCustomerRepository CustomerRepository => new(this);

    public FakeServerRepository ServerRepository => new(this);

    public FakeSecretStore SecretStore => new(this);

    public FakeUserInvitationRepository UserInvitationRepository => new(this);

    public FakeEditLockRepository EditLockRepository => new(this);

    /// <summary>A real <see cref="UserAccessService"/> composed from the fake repositories.</summary>
    public UserAccessService AccessService => new(UserRepository, RoleAssignmentRepository, RoleRepository);
}

/// <summary>Mutable clock so tests can step time forward to expire locks and invitations.</summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

internal sealed class FakeUserInvitationRepository(FakeStore store) : IUserInvitationRepository
{
    public Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        store.Invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserInvitation>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserInvitation>>(store.Invitations.Where(i => i.UserId == userId).ToList());

    public Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Invitations.SingleOrDefault(i => i.TokenHash == tokenHash));

    public void Remove(UserInvitation invitation) => store.Invitations.Remove(invitation);
}

internal sealed class FakeEditLockRepository(FakeStore store) : IEditLockRepository
{
    public Task<EditLock?> FindAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(store.EditLocks
            .SingleOrDefault(l => l.ResourceType == resourceType && l.ResourceId == resourceId));

    public Task AddAsync(EditLock editLock, CancellationToken cancellationToken = default)
    {
        store.EditLocks.Add(editLock);
        return Task.CompletedTask;
    }

    public void Remove(EditLock editLock) => store.EditLocks.Remove(editLock);
}

internal sealed class StubInvitationLinkBuilder : IInvitationLinkBuilder
{
    public string BuildAcceptLink(string rawToken) => $"https://iris.test/invitations/accept?token={rawToken}";
}

internal sealed class RecordingInvitationNotifier : IInvitationNotifier
{
    public List<InvitationNotification> Sent { get; } = [];

    public Task SendAsync(InvitationNotification notification, CancellationToken cancellationToken = default)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork(FakeStore store) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        store.SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeUserRepository(FakeStore store) : IUserRepository
{
    public Task<User?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Users.SingleOrDefault(u => u.ExternalId == externalId));

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Users
            .FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Users.SingleOrDefault(u => u.Id == userId));

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(store.Users.ToList());

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        store.Users.Add(user);
        return Task.CompletedTask;
    }

    public void Remove(User user)
    {
        store.Users.Remove(user);
        store.Assignments.RemoveAll(a => a.UserId == user.Id);
    }
}

internal sealed class FakeRoleRepository(FakeStore store) : IRoleRepository
{
    public Task<IReadOnlyList<Role>> GetByIdsAsync(
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = roleIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Role>>(store.Roles.Where(r => ids.Contains(r.Id)).ToList());
    }

    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Role>>(store.Roles.OrderBy(r => r.Key).ToList());

    public Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Roles.SingleOrDefault(r => r.Key == key));
}

internal sealed class FakeRoleAssignmentRepository(FakeStore store) : IRoleAssignmentRepository
{
    public Task<IReadOnlyList<RoleAssignment>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleAssignment>>(store.Assignments.Where(a => a.UserId == userId).ToList());

    public Task<IReadOnlyList<RoleAssignment>> GetForUsersAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<RoleAssignment>>(
            store.Assignments.Where(a => ids.Contains(a.UserId)).ToList());
    }

    public Task<RoleAssignment?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Assignments.SingleOrDefault(a => a.Id == assignmentId));

    public Task<bool> ExistsAsync(
        Guid userId,
        Guid roleId,
        AccessScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Assignments.Any(a =>
            a.UserId == userId && a.RoleId == roleId && a.Scope.Equals(scope)));

    public Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken = default)
    {
        store.Assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public void Remove(RoleAssignment assignment) => store.Assignments.Remove(assignment);
}

internal sealed class FakeCustomerRepository(FakeStore store) : ICustomerRepository
{
    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Customer>>(store.Customers.ToList());

    public Task<Customer?> GetAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Customers.SingleOrDefault(c => c.Id == customerId));

    public Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Customers.SingleOrDefault(c => c.Id == customerId));

    public Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Customers.Any(c => c.Key == key));

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        store.Customers.Add(customer);
        return Task.CompletedTask;
    }
}

internal sealed class FakeServerRepository(FakeStore store) : IServerRepository
{
    public Task<IReadOnlyList<ServerNode>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ServerNode>>(store.Servers.ToList());

    public Task<ServerNode?> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Servers.SingleOrDefault(s => s.Id == serverId));

    public Task<ServerNode?> GetForUpdateAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Servers.SingleOrDefault(s => s.Id == serverId));

    public Task AddAsync(ServerNode server, CancellationToken cancellationToken = default)
    {
        store.Servers.Add(server);
        return Task.CompletedTask;
    }

    public void Remove(ServerNode server) => store.Servers.Remove(server);
}

/// <summary>Fake stand-in for OpenBao: records what was stored so tests can assert the raw secret never reaches the DB.</summary>
internal sealed class FakeSecretStore(FakeStore store) : ISecretStore
{
    public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default)
    {
        var reference = $"fake-secret:{logicalPath}";
        store.SecretsByReference[reference] = secretValue;
        return Task.FromResult(reference);
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        store.SecretsByReference.Remove(reference);
        return Task.CompletedTask;
    }
}

/// <summary>Reversible non-crypto stand-in for the PBKDF2 hasher.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string password, string hash) => hash == $"hash:{password}";
}

internal sealed class StubCurrentUser(string externalId, string email, string displayName) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public string? ExternalId { get; } = externalId;

    public string? Email { get; } = email;

    public string? DisplayName { get; } = displayName;
}
