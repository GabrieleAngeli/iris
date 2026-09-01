using Iris.Domain.Access;

namespace Iris.Domain.Tests.Access;

public sealed class UserInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_sets_expiry_from_the_lifetime_and_starts_pending()
    {
        var invitation = UserInvitation.Issue(
            Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now, TimeSpan.FromDays(7));

        Assert.Equal(Now.AddDays(7), invitation.ExpiresAtUtc);
        Assert.Null(invitation.ConsumedAtUtc);
        Assert.True(invitation.IsPending(Now));
        Assert.False(invitation.IsPending(Now.AddDays(8)));
    }

    [Fact]
    public void Issue_rejects_a_non_positive_lifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UserInvitation.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now, TimeSpan.Zero));
    }

    [Fact]
    public void Consume_is_a_one_shot_and_rejects_an_expired_token()
    {
        var invitation = UserInvitation.Issue(
            Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now, TimeSpan.FromDays(1));

        invitation.Consume(Now.AddHours(1));
        Assert.Equal(Now.AddHours(1), invitation.ConsumedAtUtc);
        Assert.Throws<InvalidOperationException>(() => invitation.Consume(Now.AddHours(2)));

        var stale = UserInvitation.Issue(
            Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now, TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => stale.Consume(Now.AddHours(1)));
    }
}

public sealed class EditLockTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    [Fact]
    public void Acquire_records_the_holder_and_a_ttl_bound_expiry()
    {
        var holder = Guid.NewGuid();
        var editLock = EditLock.Acquire(Guid.NewGuid(), "user", Guid.NewGuid(), holder, "Alice", Now, Ttl);

        Assert.True(editLock.IsHeldBy(holder));
        Assert.Equal(Now.Add(Ttl), editLock.ExpiresAtUtc);
        Assert.False(editLock.IsExpired(Now.AddMinutes(1)));
        Assert.True(editLock.IsExpired(Now.AddMinutes(3)));
    }

    [Fact]
    public void Refresh_pushes_the_expiry_out()
    {
        var editLock = EditLock.Acquire(Guid.NewGuid(), "user", Guid.NewGuid(), Guid.NewGuid(), "Alice", Now, Ttl);

        editLock.Refresh(Now.AddMinutes(1), Ttl);

        Assert.Equal(Now.AddMinutes(1).Add(Ttl), editLock.ExpiresAtUtc);
        Assert.Equal(Now.AddMinutes(1), editLock.RefreshedAtUtc);
    }

    [Fact]
    public void TakeOver_swaps_the_holder_and_resets_the_clock()
    {
        var editLock = EditLock.Acquire(Guid.NewGuid(), "server", Guid.NewGuid(), Guid.NewGuid(), "Alice", Now, Ttl);
        var bob = Guid.NewGuid();

        editLock.TakeOver(bob, "Bob", Now.AddMinutes(5), Ttl);

        Assert.True(editLock.IsHeldBy(bob));
        Assert.Equal("Bob", editLock.HolderDisplayName);
        Assert.Equal(Now.AddMinutes(5), editLock.AcquiredAtUtc);
        Assert.Equal(Now.AddMinutes(5).Add(Ttl), editLock.ExpiresAtUtc);
    }
}
