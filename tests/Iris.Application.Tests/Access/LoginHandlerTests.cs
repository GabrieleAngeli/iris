using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Access;

public sealed class LoginHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static LoginHandler Handler(FakeStore store) => new(
        store.UserRepository,
        new SessionIssuer(store.UserSessionRepository, new FakeClock(Now)),
        new FakePasswordHasher(),
        store.UnitOfWork);

    private static User WithPassword(FakeStore store, string email = "pat@contoso.example", bool active = true)
    {
        var user = new User(Guid.NewGuid(), $"ext-{Guid.NewGuid():N}", email, "Pat");
        user.SetPassword(new FakePasswordHasher().Hash("correct-horse"), Now);
        if (!active)
        {
            user.Deactivate();
        }

        store.WithUser(user);
        return user;
    }

    [Fact]
    public async Task Login_succeeds_and_issues_a_session()
    {
        var store = new FakeStore();
        var user = WithPassword(store);

        var result = await Handler(store).HandleAsync(new LoginCommand(user.Email, "correct-horse"));

        Assert.NotEmpty(result.Token);
        Assert.Equal(Now.Add(LoginHandler.SessionLifetime), result.ExpiresAtUtc);

        var session = Assert.Single(store.Sessions);
        Assert.Equal(user.Id, session.UserId);
        Assert.True(session.IsValid(Now));
        Assert.NotEqual(result.Token, session.TokenHash); // the raw token is never what's stored
    }

    [Fact]
    public async Task Login_rejects_unknown_email_and_wrong_password_identically()
    {
        var store = new FakeStore();
        var user = WithPassword(store);
        var handler = Handler(store);

        var unknownEx = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new LoginCommand("nobody@contoso.example", "whatever")));
        var wrongPasswordEx = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new LoginCommand(user.Email, "wrong-password")));

        Assert.Equal(unknownEx.Message, wrongPasswordEx.Message);
        Assert.Empty(store.Sessions);
    }

    [Fact]
    public async Task Login_rejects_a_deactivated_account()
    {
        var store = new FakeStore();
        var user = WithPassword(store, active: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store).HandleAsync(new LoginCommand(user.Email, "correct-horse")));
    }

    [Fact]
    public async Task Login_rejects_an_account_with_no_local_password()
    {
        var store = new FakeStore();
        var user = new User(Guid.NewGuid(), "ext-nopw", "nopw@contoso.example", "No Pw");
        store.WithUser(user);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store).HandleAsync(new LoginCommand(user.Email, "anything")));
    }
}
