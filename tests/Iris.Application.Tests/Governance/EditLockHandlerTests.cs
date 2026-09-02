using Iris.Application.Common;
using Iris.Application.Governance;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Governance;

public sealed class EditLockHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Target = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class Fixture
    {
        public FakeStore Store { get; } = new();

        public FakeClock Clock { get; } = new(Now);

        public User Alice { get; }

        public User Bob { get; }

        public Fixture()
        {
            Alice = new User(Guid.NewGuid(), "ext-alice", "alice@iris.local", "Alice");
            Bob = new User(Guid.NewGuid(), "ext-bob", "bob@iris.local", "Bob");
            Store.WithUser(Alice).WithUser(Bob);
        }

        public void MakeBobPlatformAdmin()
        {
            var role = new Role(Guid.NewGuid(), "platform-admin", "Platform admin", isBuiltIn: true);
            role.Grant(PermissionId.Parse(Permissions.PlatformAdmin));
            Store.WithRole(role).WithAssignment(new RoleAssignment(Guid.NewGuid(), Bob.Id, role.Id, AccessScope.Global()));
        }

        public AcquireEditLockHandler Acquire(User caller) => new(
            Store.EditLockRepository, Store.AccessService, Caller(caller), Clock, Store.UnitOfWork);

        public ReleaseEditLockHandler Release(User caller) => new(
            Store.EditLockRepository, Store.AccessService, Caller(caller), Clock, Store.UnitOfWork);

        public GetEditLockHandler Get(User caller) => new(
            Store.EditLockRepository, Store.AccessService, Caller(caller), Clock);

        private static StubCurrentUser Caller(User user) => new(user.ExternalId, user.Email, user.DisplayName);
    }

    [Fact]
    public async Task Acquire_takes_the_lock_then_refreshes_it_for_the_same_holder()
    {
        var f = new Fixture();

        var first = await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("user", Target));

        Assert.True(first.Mine);
        Assert.Equal(f.Alice.Id, first.HolderUserId);
        Assert.Equal(Now.Add(EditLockPolicy.Ttl), first.ExpiresAtUtc);
        Assert.Single(f.Store.EditLocks);

        f.Clock.Advance(TimeSpan.FromSeconds(30));
        var refreshed = await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("user", Target));

        Assert.True(refreshed.Mine);
        Assert.Equal(Now.AddSeconds(30).Add(EditLockPolicy.Ttl), refreshed.ExpiresAtUtc);
        Assert.Single(f.Store.EditLocks);
    }

    [Fact]
    public async Task Acquire_reports_the_other_holder_without_taking_over()
    {
        var f = new Fixture();
        await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("user", Target));

        var seenByBob = await f.Acquire(f.Bob).HandleAsync(new AcquireEditLockCommand("user", Target));

        Assert.False(seenByBob.Mine);
        Assert.Equal("Alice", seenByBob.HolderDisplayName);
        Assert.Equal(f.Alice.Id, Assert.Single(f.Store.EditLocks).HolderUserId);
    }

    [Fact]
    public async Task Acquire_takes_over_a_lapsed_lock()
    {
        var f = new Fixture();
        await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("user", Target));

        f.Clock.Advance(EditLockPolicy.Ttl + TimeSpan.FromMinutes(1));
        var takenOver = await f.Acquire(f.Bob).HandleAsync(new AcquireEditLockCommand("user", Target));

        Assert.True(takenOver.Mine);
        Assert.Equal(f.Bob.Id, takenOver.HolderUserId);
        Assert.Single(f.Store.EditLocks);
    }

    [Fact]
    public async Task Release_is_holder_only_unless_a_platform_admin_forces_it()
    {
        var f = new Fixture();
        f.MakeBobPlatformAdmin();
        await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("server", Target));

        await Assert.ThrowsAsync<ConflictException>(() =>
            f.Release(f.Bob).HandleAsync(new ReleaseEditLockCommand("server", Target, Force: false)));

        await f.Release(f.Bob).HandleAsync(new ReleaseEditLockCommand("server", Target, Force: true));
        Assert.Empty(f.Store.EditLocks);

        // idempotent when nothing is locked
        await f.Release(f.Alice).HandleAsync(new ReleaseEditLockCommand("server", Target, Force: false));
    }

    [Fact]
    public async Task Get_returns_null_when_free_or_lapsed()
    {
        var f = new Fixture();

        Assert.Null(await f.Get(f.Bob).HandleAsync(new GetEditLockQuery("user", Target)));

        await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("user", Target));
        var held = await f.Get(f.Bob).HandleAsync(new GetEditLockQuery("user", Target));
        Assert.NotNull(held);
        Assert.False(held!.Mine);

        f.Clock.Advance(EditLockPolicy.Ttl + TimeSpan.FromMinutes(1));
        Assert.Null(await f.Get(f.Bob).HandleAsync(new GetEditLockQuery("user", Target)));
    }

    [Fact]
    public async Task Unknown_resource_type_is_rejected()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() =>
            f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand("widget", Target)));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("server")]
    [InlineData("customer")]
    [InlineData("application")]
    public async Task Every_lockable_resource_type_is_accepted(string resourceType)
    {
        var f = new Fixture();

        var acquired = await f.Acquire(f.Alice).HandleAsync(new AcquireEditLockCommand(resourceType, Target));

        Assert.True(acquired.Mine);
        Assert.Equal(resourceType, acquired.ResourceType);
    }
}
