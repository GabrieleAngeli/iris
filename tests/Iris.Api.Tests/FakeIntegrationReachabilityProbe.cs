using Iris.Application.Abstractions;

namespace Iris.Api.Tests;

/// <summary>
/// Always-succeeds stand-in wired into every <see cref="IrisApiFactory"/> so API tests never make
/// a real HTTP call to OpenBao/AWX during <c>/setup/complete</c>. The failure paths themselves are
/// covered with full control at the handler level (<c>Iris.Application.Tests/Setup/SetupHandlerTests.cs</c>)
/// and the probe's own HTTP behavior in <c>Iris.Infrastructure.Tests</c>.
/// </summary>
internal sealed class FakeIntegrationReachabilityProbe : IIntegrationReachabilityProbe
{
    public Task ProbeOpenBaoAsync(string endpoint, string? token, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<AwxProbeResult> ProbeAwxAsync(
        string endpoint, string? token, string? oAuthClientId, string? oAuthClientSecret, string? refreshToken,
        CancellationToken cancellationToken = default) =>
        // Echo a rotated pair when OAuth refresh creds were supplied, so the end-to-end path
        // (probe -> persist the rotated pair) is exercised without real HTTP.
        Task.FromResult(string.IsNullOrWhiteSpace(refreshToken)
            ? new AwxProbeResult(null, null)
            : new AwxProbeResult("fake-fresh-access", "fake-rotated-refresh"));
}
