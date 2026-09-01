using Iris.Domain.Access;

namespace Iris.Domain.Tests.Access;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_directly_created_user_is_provisioned_and_not_prompted_for_a_password()
    {
        var user = new User(Guid.NewGuid(), "oid-1", "u@iris.local", "User One");

        Assert.True(user.IsProvisioned);
        Assert.False(user.PasswordSetupPending);
        Assert.False(user.HasPassword);
    }

    [Fact]
    public void An_invited_user_is_pending_provisioning_and_a_password_prompt()
    {
        var user = User.Invite(Guid.NewGuid(), "pending@contoso.example", "Pat Pending");

        Assert.False(user.IsProvisioned);
        Assert.True(user.PasswordSetupPending);
    }

    [Fact]
    public void SetPassword_stores_the_hash_and_clears_the_prompt()
    {
        var user = User.Invite(Guid.NewGuid(), "pending@contoso.example", "Pat Pending");

        user.SetPassword("pbkdf2-sha256$1$s$k", Now);

        Assert.True(user.HasPassword);
        Assert.Equal("pbkdf2-sha256$1$s$k", user.PasswordHash);
        Assert.Equal(Now, user.PasswordUpdatedAtUtc);
        Assert.False(user.PasswordSetupPending);
    }

    [Fact]
    public void SkipPasswordSetup_clears_the_prompt_without_a_hash()
    {
        var user = User.Invite(Guid.NewGuid(), "pending@contoso.example", "Pat Pending");

        user.SkipPasswordSetup();

        Assert.False(user.PasswordSetupPending);
        Assert.False(user.HasPassword);
    }
}
