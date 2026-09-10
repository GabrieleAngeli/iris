using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Secrets;

/// <summary>
/// The one <see cref="ISecretStore"/> the whole app resolves. Delegates to the encrypted
/// fallback vault until <see cref="PromoteToOpenBaoAsync"/> switches it to real OpenBao at
/// runtime (once OpenBao's token is finally in memory after an admin unlock) — no restart.
/// If a token was supplied via config/env at startup it begins on OpenBao directly, exactly as
/// before this type existed.
/// </summary>
internal sealed class SwitchableSecretStore : ISecretStore, ISecretStorePromotion, IDisposable
{
    private const string ProbeLogicalPath = "iris/_promotion-probe";

    private readonly EncryptedFallbackSecretStore _fallback;
    private readonly Func<OpenBaoOptions, ISecretStore> _openBaoFactory;
    private readonly SemaphoreSlim _promoteLock = new(1, 1);

    private volatile ISecretStore _active;
    private ISecretStore? _openBao;

    public SwitchableSecretStore(EncryptedFallbackSecretStore fallback, OpenBaoOptions bootstrapOptions)
        : this(fallback, bootstrapOptions, options => new OpenBaoSecretStore(options))
    {
    }

    /// <summary>Test seam: swap in a fake OpenBao store instead of a real HTTP one.</summary>
    internal SwitchableSecretStore(
        EncryptedFallbackSecretStore fallback,
        OpenBaoOptions bootstrapOptions,
        Func<OpenBaoOptions, ISecretStore> openBaoFactory)
    {
        _fallback = fallback;
        _openBaoFactory = openBaoFactory;

        if (bootstrapOptions.IsSecretStoreConfigured)
        {
            // A config/env token bootstraps OpenBao immediately — preserve the pre-existing
            // behavior with no verification round-trip.
            _openBao = _openBaoFactory(bootstrapOptions);
            _active = _openBao;
        }
        else
        {
            _active = _fallback;
        }
    }

    public bool IsOpenBaoActive => ReferenceEquals(_active, _openBao) && _openBao is not null;

    public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default) =>
        _active.StoreAsync(logicalPath, secretValue, cancellationToken);

    public Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default) =>
        _active.RetrieveAsync(reference, cancellationToken);

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default) =>
        _active.DeleteAsync(reference, cancellationToken);

    public async Task<int> PromoteToOpenBaoAsync(
        string endpoint,
        string token,
        string mountPath,
        bool useKvV2,
        CancellationToken cancellationToken = default)
    {
        await _promoteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOpenBaoActive)
            {
                return 0;
            }

            var target = _openBaoFactory(new OpenBaoOptions
            {
                Endpoint = endpoint,
                Token = token,
                MountPath = string.IsNullOrWhiteSpace(mountPath) ? "secret" : mountPath,
                UseKvV2 = useKvV2,
            });

            try
            {
                await VerifyAsync(target, cancellationToken).ConfigureAwait(false);

                var migrated = 0;
                foreach (var (reference, plaintext) in _fallback.Snapshot())
                {
                    await target.StoreAsync(ToLogicalPath(reference), plaintext, cancellationToken).ConfigureAwait(false);
                    migrated++;
                }

                _openBao = target;
                _active = target;
                return migrated;
            }
            catch (SecretStorePromotionException)
            {
                (target as IDisposable)?.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                (target as IDisposable)?.Dispose();
                throw new SecretStorePromotionException($"Could not promote to OpenBao at {endpoint}: {ex.Message}");
            }
        }
        finally
        {
            _promoteLock.Release();
        }
    }

    private static async Task VerifyAsync(ISecretStore target, CancellationToken cancellationToken)
    {
        // A full round-trip proves reachability, the token, and that the KV mount actually works.
        var probeValue = Guid.NewGuid().ToString("N");
        var reference = await target.StoreAsync(ProbeLogicalPath, probeValue, cancellationToken).ConfigureAwait(false);
        var read = await target.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
        await target.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(read, probeValue, StringComparison.Ordinal))
        {
            throw new SecretStorePromotionException(
                "OpenBao accepted the write but read back a different value — check the mount path and KV version.");
        }
    }

    private static string ToLogicalPath(string reference)
    {
        const string fallbackPrefix = "mock-openbao:";
        return reference.StartsWith(fallbackPrefix, StringComparison.Ordinal)
            ? reference[fallbackPrefix.Length..].Trim('/')
            : reference.Trim('/');
    }

    public void Dispose()
    {
        (_openBao as IDisposable)?.Dispose();
        _promoteLock.Dispose();
    }
}
