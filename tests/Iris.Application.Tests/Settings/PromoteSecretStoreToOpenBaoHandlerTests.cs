using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;

namespace Iris.Application.Tests.Settings;

public sealed class PromoteSecretStoreToOpenBaoHandlerTests
{
    private static PromoteSecretStoreToOpenBaoHandler Handler(FakeStore store, FakeSecretStorePromotion promotion) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, promotion);

    private static Task SeedOpenBaoAsync(FakeStore store, string? token) =>
        new SaveOpenBaoIntegrationSettingsHandler(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork)
            .HandleAsync(new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example:8200", token, "secret", UseKvV2: true));

    [Fact]
    public async Task Fails_when_OpenBao_is_not_configured()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, new FakeSecretStorePromotion()).HandleAsync(new PromoteSecretStoreToOpenBaoCommand()));
    }

    [Fact]
    public async Task Fails_when_the_OpenBao_token_cannot_be_resolved()
    {
        var store = new FakeStore();
        await SeedOpenBaoAsync(store, token: null); // endpoint saved, no token stored

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, new FakeSecretStorePromotion()).HandleAsync(new PromoteSecretStoreToOpenBaoCommand()));

        Assert.Contains("token", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_op_when_OpenBao_is_already_the_active_store()
    {
        var store = new FakeStore();
        var promotion = new FakeSecretStorePromotion { IsOpenBaoActive = true };

        var result = await Handler(store, promotion).HandleAsync(new PromoteSecretStoreToOpenBaoCommand());

        Assert.False(result.Promoted);
        Assert.Empty(promotion.Promotions);
    }

    [Fact]
    public async Task Promotes_with_the_resolved_token_and_returns_the_migrated_count()
    {
        var store = new FakeStore();
        await SeedOpenBaoAsync(store, token: "s.roottoken");
        var promotion = new FakeSecretStorePromotion { MigratedCount = 5 };

        var result = await Handler(store, promotion).HandleAsync(new PromoteSecretStoreToOpenBaoCommand());

        Assert.True(result.Promoted);
        Assert.Equal(5, result.MigratedSecrets);
        var call = Assert.Single(promotion.Promotions);
        Assert.Equal("https://openbao.example:8200", call.Endpoint);
        Assert.Equal("s.roottoken", call.Token);
        Assert.Equal("secret", call.MountPath);
        Assert.True(call.UseKvV2);
    }

    [Fact]
    public async Task Surfaces_a_promotion_failure_as_a_validation_error()
    {
        var store = new FakeStore();
        await SeedOpenBaoAsync(store, token: "s.roottoken");
        var promotion = new FakeSecretStorePromotion
        {
            FailWith = new SecretStorePromotionException("OpenBao sealed"),
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store, promotion).HandleAsync(new PromoteSecretStoreToOpenBaoCommand()));

        Assert.Contains("sealed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
