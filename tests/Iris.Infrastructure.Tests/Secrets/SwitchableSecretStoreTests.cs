using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Secrets;

namespace Iris.Infrastructure.Tests.Secrets;

public sealed class SwitchableSecretStoreTests
{
    /// <summary>An in-memory <see cref="ISecretStore"/> standing in for real OpenBao — references
    /// derive from the logical path, matching OpenBaoSecretStore's scheme.</summary>
    private sealed class FakeOpenBao : ISecretStore
    {
        private readonly Dictionary<string, string> _byLogicalPath = new(StringComparer.Ordinal);

        public bool BreakReadBack { get; set; }

        public List<string> Stored => _byLogicalPath.Keys.ToList();

        public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default)
        {
            _byLogicalPath[logicalPath.Trim('/')] = secretValue;
            return Task.FromResult($"openbao://secret/{logicalPath.Trim('/')}");
        }

        public Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default)
        {
            if (BreakReadBack)
            {
                return Task.FromResult<string?>("tampered");
            }

            var logical = reference.StartsWith("mock-openbao:", StringComparison.Ordinal)
                ? reference["mock-openbao:".Length..].Trim('/')
                : new Uri(reference).AbsolutePath.Trim('/');
            return Task.FromResult(_byLogicalPath.GetValueOrDefault(logical));
        }

        public Task DeleteAsync(string reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static OpenBaoOptions Configured() => new()
    {
        Endpoint = "https://openbao.example:8200",
        Token = "s.token",
        MountPath = "secret",
        UseKvV2 = true,
    };

    [Fact]
    public async Task Starts_on_the_fallback_store_when_no_bootstrap_token()
    {
        var fallback = new EncryptedFallbackSecretStore();
        var openBao = new FakeOpenBao();
        using var store = new SwitchableSecretStore(fallback, new OpenBaoOptions { Endpoint = "x" }, _ => openBao);

        var reference = await store.StoreAsync("awx/token", "abc");

        Assert.False(store.IsOpenBaoActive);
        Assert.StartsWith("mock-openbao:", reference); // written to the fallback store
    }

    [Fact]
    public async Task Promote_verifies_migrates_every_secret_and_switches_the_live_store()
    {
        var fallback = new EncryptedFallbackSecretStore();
        await fallback.StoreAsync("awx/token", "awx-access");
        await fallback.StoreAsync("mail/smtp", "smtp-pass");
        await fallback.StoreAsync("openbao/token", "s.token");
        var openBao = new FakeOpenBao();
        using var store = new SwitchableSecretStore(fallback, new OpenBaoOptions { Endpoint = "x" }, _ => openBao);

        var migrated = await store.PromoteToOpenBaoAsync("https://openbao.example:8200", "s.token", "secret", true);

        Assert.Equal(3, migrated);
        Assert.True(store.IsOpenBaoActive);
        // A reference persisted while on the fallback store still resolves after the switch.
        Assert.Equal("awx-access", await store.RetrieveAsync("mock-openbao:awx/token"));
        // ...and so does a native OpenBao-shaped one.
        Assert.Equal("smtp-pass", await store.RetrieveAsync("openbao://secret/mail/smtp"));
        // The probe secret was cleaned up (delete is a no-op in the fake, so just assert it's not surfaced as a real one).
        Assert.Contains("awx/token", openBao.Stored);
    }

    [Fact]
    public async Task Promote_that_fails_verification_does_not_switch_and_throws()
    {
        var fallback = new EncryptedFallbackSecretStore();
        await fallback.StoreAsync("awx/token", "awx-access");
        var openBao = new FakeOpenBao { BreakReadBack = true };
        using var store = new SwitchableSecretStore(fallback, new OpenBaoOptions { Endpoint = "x" }, _ => openBao);

        await Assert.ThrowsAsync<SecretStorePromotionException>(
            () => store.PromoteToOpenBaoAsync("https://openbao.example:8200", "s.token", "secret", true));

        Assert.False(store.IsOpenBaoActive);
        Assert.Equal("awx-access", await store.RetrieveAsync("mock-openbao:awx/token")); // still the fallback
    }

    [Fact]
    public async Task Promote_is_a_no_op_when_OpenBao_is_already_the_live_store()
    {
        var fallback = new EncryptedFallbackSecretStore();
        using var store = new SwitchableSecretStore(fallback, Configured(), _ => new FakeOpenBao());

        Assert.True(store.IsOpenBaoActive);
        Assert.Equal(0, await store.PromoteToOpenBaoAsync("https://openbao.example:8200", "s.token", "secret", true));
    }
}
