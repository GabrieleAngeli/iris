using Iris.Application.Abstractions;

namespace Iris.Api.Tests;

/// <summary>
/// Stand-in wired into every <see cref="IrisApiFactory"/> so <c>POST /system/integrations/openbao/promote</c>
/// and the unlock endpoint's best-effort auto-promote never make a real HTTP call to OpenBao.
/// Reports a fixed migrated count and flips itself "active" on the first successful call.
/// </summary>
internal sealed class FakeSecretStorePromotion : ISecretStorePromotion
{
    public bool IsOpenBaoActive { get; private set; }

    public Task<int> PromoteToOpenBaoAsync(
        string endpoint, string token, string mountPath, bool useKvV2, CancellationToken cancellationToken = default)
    {
        IsOpenBaoActive = true;
        return Task.FromResult(2);
    }
}
