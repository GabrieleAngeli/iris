using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Access;

public sealed class PasswordHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static (SetMyPasswordHandler Set, SkipMyPasswordSetupHandler Skip, FakeStore Store, User User) Build()
    {
        var store = new FakeStore();
        var user = User.Invite(Guid.NewGuid(), "pending@contoso.example", "Pat Pending");
        // Invite() gives a synthetic ExternalId; point the stub caller at it.
        store.WithUser(user);

        var caller = new StubCurrentUser(user.ExternalId, user.Email, user.DisplayName);
        var provisioning = new UserProvisioningService(store.UserRepository, store.UnitOfWork);
        var set = new SetMyPasswordHandler(caller, provisioning, new FakePasswordHasher(), new FakeClock(Now), store.UnitOfWork);
        var skip = new SkipMyPasswordSetupHandler(caller, provisioning, store.UnitOfWork);
        return (set, skip, store, user);
    }

    [Fact]
    public async Task SetMyPassword_hashes_the_password_and_clears_the_prompt()
    {
        var (set, _, store, user) = Build();

        await set.HandleAsync(new SetMyPasswordCommand("hunter2!!", null));

        Assert.True(user.HasPassword);
        Assert.Equal("hash:hunter2!!", user.PasswordHash);
        Assert.False(user.PasswordSetupPending);
        Assert.Equal(1, store.SaveChangesCalls);
    }

    [Fact]
    public async Task SetMyPassword_rejects_a_short_password()
    {
        var (set, _, _, _) = Build();

        await Assert.ThrowsAsync<ValidationException>(() =>
            set.HandleAsync(new SetMyPasswordCommand("short", null)));
    }

    [Fact]
    public async Task Changing_an_existing_password_requires_the_current_one()
    {
        var (set, _, _, user) = Build();
        await set.HandleAsync(new SetMyPasswordCommand("firstpass1", null));

        await Assert.ThrowsAsync<ValidationException>(() =>
            set.HandleAsync(new SetMyPasswordCommand("secondpass1", "wrong")));

        await set.HandleAsync(new SetMyPasswordCommand("secondpass1", "firstpass1"));
        Assert.Equal("hash:secondpass1", user.PasswordHash);
    }

    [Fact]
    public async Task SkipMyPasswordSetup_clears_the_prompt_without_a_hash()
    {
        var (_, skip, store, user) = Build();

        await skip.HandleAsync(new SkipMyPasswordSetupCommand());

        Assert.False(user.PasswordSetupPending);
        Assert.False(user.HasPassword);
        Assert.Equal(1, store.SaveChangesCalls);
    }
}
