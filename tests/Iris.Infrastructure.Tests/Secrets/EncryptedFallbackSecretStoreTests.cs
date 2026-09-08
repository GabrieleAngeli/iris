using Iris.Infrastructure.Secrets;

namespace Iris.Infrastructure.Tests.Secrets;

/// <summary>
/// Regression guard: this class replaces <c>InMemorySecretStore</c> as the fallback
/// <c>ISecretStore</c>, and every existing caller must see identical behavior from the
/// <c>ISecretStore</c> side — all the new encrypted-persistence behavior lives in
/// <c>FallbackSecretVault</c>/<c>IFallbackSecretCache</c> instead, exercised separately.
/// </summary>
public sealed class EncryptedFallbackSecretStoreTests
{
    [Fact]
    public async Task StoreAsync_returns_a_mock_reference_and_RetrieveAsync_resolves_it()
    {
        var store = new EncryptedFallbackSecretStore();

        var reference = await store.StoreAsync("openbao/token", "s.some-token");

        Assert.Equal("mock-openbao:openbao/token", reference);
        Assert.Equal("s.some-token", await store.RetrieveAsync(reference));
    }

    [Fact]
    public async Task RetrieveAsync_returns_null_for_an_unknown_reference()
    {
        var store = new EncryptedFallbackSecretStore();

        Assert.Null(await store.RetrieveAsync("mock-openbao:nothing-here"));
    }

    [Fact]
    public async Task DeleteAsync_removes_the_value()
    {
        var store = new EncryptedFallbackSecretStore();
        var reference = await store.StoreAsync("openbao/token", "s.some-token");

        await store.DeleteAsync(reference);

        Assert.Null(await store.RetrieveAsync(reference));
    }

    [Fact]
    public async Task StoreAsync_overwrites_the_same_logical_path()
    {
        var store = new EncryptedFallbackSecretStore();

        var first = await store.StoreAsync("mail/smtp", "old-password");
        var second = await store.StoreAsync("mail/smtp", "new-password");

        Assert.Equal(first, second);
        Assert.Equal("new-password", await store.RetrieveAsync(second));
    }

    [Fact]
    public async Task IFallbackSecretCache_view_reflects_the_same_dictionary_ISecretStore_writes_to()
    {
        var store = new EncryptedFallbackSecretStore();
        IFallbackSecretCache cache = store;

        var reference = await store.StoreAsync("openbao/token", "s.some-token");

        Assert.True(cache.Contains(reference));
        Assert.Equal("s.some-token", cache.Snapshot()[reference]);

        cache.Populate("mock-openbao:restored-ref", "restored-value");
        Assert.Equal("restored-value", await store.RetrieveAsync("mock-openbao:restored-ref"));
    }
}
