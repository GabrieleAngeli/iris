using Iris.Application.Common;
using Iris.Application.Governance;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Governance;

public sealed class InvitationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static (IssueUserInvitationHandler Handler, FakeStore Store, RecordingInvitationNotifier Notifier) Build(
        out User target)
    {
        var store = new FakeStore();
        var admin = new User(Guid.NewGuid(), "ext-admin", "admin@iris.local", "Admin");
        target = User.Invite(Guid.NewGuid(), "pending@contoso.example", "Pat Pending");
        store.WithUser(admin).WithUser(target);

        var notifier = new RecordingInvitationNotifier();
        var handler = new IssueUserInvitationHandler(
            store.UserRepository,
            store.UserInvitationRepository,
            new StubInvitationLinkBuilder(),
            notifier,
            new StubCurrentUser("ext-admin", "admin@iris.local", "Admin"),
            new FakeClock(Now),
            store.UnitOfWork);

        return (handler, store, notifier);
    }

    [Fact]
    public async Task IssueUserInvitation_mints_a_pending_token_and_notifies()
    {
        var (handler, store, notifier) = Build(out var target);

        var result = await handler.HandleAsync(new IssueUserInvitationCommand(target.Id));

        Assert.NotEmpty(result.Token);
        Assert.Contains(result.Token, result.AcceptLink, StringComparison.Ordinal);
        Assert.Equal(Now.Add(IssueUserInvitationHandler.Lifetime), result.ExpiresAtUtc);

        var stored = Assert.Single(store.Invitations);
        Assert.Equal(IssueUserInvitationHandler.HashToken(result.Token), stored.TokenHash);
        Assert.Null(stored.ConsumedAtUtc);
        Assert.True(stored.IsPending(Now));

        var sent = Assert.Single(notifier.Sent);
        Assert.Equal(target.Email, sent.Email);
        Assert.Equal(result.AcceptLink, sent.AcceptLink);
    }

    [Fact]
    public async Task IssueUserInvitation_supersedes_the_previous_token()
    {
        var (handler, store, _) = Build(out var target);

        var first = await handler.HandleAsync(new IssueUserInvitationCommand(target.Id));
        var second = await handler.HandleAsync(new IssueUserInvitationCommand(target.Id));

        Assert.NotEqual(first.Token, second.Token);
        var stored = Assert.Single(store.Invitations);
        Assert.Equal(IssueUserInvitationHandler.HashToken(second.Token), stored.TokenHash);
    }

    [Fact]
    public async Task IssueUserInvitation_unknown_user_is_NotFound()
    {
        var (handler, _, _) = Build(out _);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new IssueUserInvitationCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task AcceptInvitation_sets_the_password_and_consumes_the_token()
    {
        var (issue, store, _) = Build(out var target);
        var issued = await issue.HandleAsync(new IssueUserInvitationCommand(target.Id));

        var accept = new AcceptInvitationHandler(
            store.UserRepository, store.UserInvitationRepository, new FakePasswordHasher(), new FakeClock(Now), store.UnitOfWork);

        var result = await accept.HandleAsync(new AcceptInvitationCommand(issued.Token, "brand-new-secret"));

        Assert.Equal(target.Email, result.Email);
        Assert.True(target.HasPassword);
        Assert.True(new FakePasswordHasher().Verify("brand-new-secret", target.PasswordHash!));

        var stored = Assert.Single(store.Invitations);
        Assert.Equal(Now, stored.ConsumedAtUtc);
        Assert.False(stored.IsPending(Now));
    }

    [Fact]
    public async Task AcceptInvitation_rejects_unknown_consumed_or_expired_tokens()
    {
        var (issue, store, _) = Build(out var target);
        var issued = await issue.HandleAsync(new IssueUserInvitationCommand(target.Id));

        var clock = new FakeClock(Now);
        var accept = new AcceptInvitationHandler(
            store.UserRepository, store.UserInvitationRepository, new FakePasswordHasher(), clock, store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            accept.HandleAsync(new AcceptInvitationCommand("not-a-real-token", "brand-new-secret")));

        // consumed
        await accept.HandleAsync(new AcceptInvitationCommand(issued.Token, "brand-new-secret"));
        await Assert.ThrowsAsync<ValidationException>(() =>
            accept.HandleAsync(new AcceptInvitationCommand(issued.Token, "another-secret")));

        // expired
        var second = User.Invite(Guid.NewGuid(), "pending2@contoso.example", "Pat Two");
        store.WithUser(second);
        var reissued = await issue.HandleAsync(new IssueUserInvitationCommand(second.Id));
        clock.Advance(IssueUserInvitationHandler.Lifetime + TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<ValidationException>(() =>
            accept.HandleAsync(new AcceptInvitationCommand(reissued.Token, "brand-new-secret")));
    }

    [Fact]
    public async Task AcceptInvitation_rejects_a_short_password()
    {
        var (issue, store, _) = Build(out var target);
        var issued = await issue.HandleAsync(new IssueUserInvitationCommand(target.Id));

        var accept = new AcceptInvitationHandler(
            store.UserRepository, store.UserInvitationRepository, new FakePasswordHasher(), new FakeClock(Now), store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            accept.HandleAsync(new AcceptInvitationCommand(issued.Token, "short")));
    }
}
