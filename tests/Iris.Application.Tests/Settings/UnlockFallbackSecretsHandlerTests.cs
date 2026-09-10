using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Settings;

public sealed class UnlockFallbackSecretsHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly FakePasswordHasher _passwordHasher = new();

    private UnlockFallbackSecretsHandler Handler(
        FakeStore store, StubCurrentUser currentUser, FakeFallbackSecretVault vault, FakeSecretStorePromotion? promotion = null) =>
        new(
            currentUser,
            new UserProvisioningService(store.UserRepository, store.UnitOfWork),
            _passwordHasher,
            vault,
            new PromoteSecretStoreToOpenBaoHandler(store.IntegrationSettingsRepository, store.SecretStore, promotion ?? new FakeSecretStorePromotion()));

    private User WithLocalPassword(FakeStore store, string password)
    {
        var user = new User(Guid.CreateVersion7(), "ext-1", "admin@iris.local", "Admin");
        user.SetPassword(_passwordHasher.Hash(password), Now);
        store.WithUser(user);
        return user;
    }

    [Fact]
    public async Task HandleAsync_rejects_the_wrong_password_and_never_calls_the_vault()
    {
        var store = new FakeStore();
        WithLocalPassword(store, "correct-password");
        var currentUser = new StubCurrentUser("ext-1", "admin@iris.local", "Admin");
        var vault = new FakeFallbackSecretVault();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, currentUser, vault).HandleAsync(new UnlockFallbackSecretsCommand("wrong-password")));

        Assert.Empty(vault.UnlockCalls);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_account_with_no_local_password()
    {
        var store = new FakeStore();
        var user = new User(Guid.CreateVersion7(), "ext-1", "sso-only@iris.local", "SSO Admin");
        store.WithUser(user);
        var currentUser = new StubCurrentUser("ext-1", "sso-only@iris.local", "SSO Admin");
        var vault = new FakeFallbackSecretVault();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, currentUser, vault).HandleAsync(new UnlockFallbackSecretsCommand("anything")));

        Assert.Contains("local password", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vault.UnlockCalls);
    }

    [Fact]
    public async Task HandleAsync_delegates_to_the_vault_with_the_verified_password_and_returns_its_result()
    {
        var store = new FakeStore();
        var user = WithLocalPassword(store, "correct-password");
        var currentUser = new StubCurrentUser("ext-1", "admin@iris.local", "Admin");
        var vault = new FakeFallbackSecretVault { UnlockResult = new(2, 1, 1) };

        var result = await Handler(store, currentUser, vault).HandleAsync(new UnlockFallbackSecretsCommand("correct-password"));

        Assert.Equal(2, result.Persisted);
        Assert.Equal(1, result.Restored);
        Assert.Equal(1, result.OwnershipTransferred);
        var call = Assert.Single(vault.UnlockCalls);
        Assert.Equal(user.Id, call.UserId);
        Assert.Equal("correct-password", call.Password);
    }
}
