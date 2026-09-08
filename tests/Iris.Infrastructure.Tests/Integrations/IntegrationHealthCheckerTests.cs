using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class IntegrationHealthCheckerTests
{
    private sealed class FakeConnector(string key, Func<IntegrationConnectorStatus> respond) : IIntegrationConnector
    {
        public string Key => key;
        public string Name => key;
        public string? Endpoint => null;

        public Task<IntegrationConnectorStatus> GetStatusAsync(bool probe = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(respond());
    }

    private sealed class ThrowingConnector(string key) : IIntegrationConnector
    {
        public string Key => key;
        public string Name => key;
        public string? Endpoint => null;

        public Task<IntegrationConnectorStatus> GetStatusAsync(bool probe = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    [Fact]
    public async Task RunOnceAsync_probes_every_connector_and_records_its_status()
    {
        var now = DateTimeOffset.UtcNow;
        var connectors = new IIntegrationConnector[]
        {
            new FakeConnector("openbao", () => new IntegrationConnectorStatus("openbao", "OpenBao", "Reachable", "https://openbao", "200 OK")),
            new FakeConnector("awx", () => new IntegrationConnectorStatus("awx", "AWX", "Not configured", null, "Endpoint required.")),
        };
        var monitor = new IntegrationHealthMonitor();
        var checker = new IntegrationHealthChecker(connectors, monitor, new FixedClock(now), NullLogger<IntegrationHealthChecker>.Instance);

        await checker.RunOnceAsync();

        var openBao = monitor.GetSnapshot("openbao");
        Assert.Equal("Reachable", openBao!.Status);
        Assert.Equal("200 OK", openBao.Message);
        Assert.Equal(now, openBao.CheckedAtUtc);

        var awx = monitor.GetSnapshot("awx");
        Assert.Equal("Not configured", awx!.Status);
    }

    [Fact]
    public async Task RunOnceAsync_isolates_a_connector_that_throws_and_still_checks_the_rest()
    {
        var connectors = new IIntegrationConnector[]
        {
            new ThrowingConnector("openbao"),
            new FakeConnector("awx", () => new IntegrationConnectorStatus("awx", "AWX", "Reachable", "https://awx", "200 OK")),
        };
        var monitor = new IntegrationHealthMonitor();
        var checker = new IntegrationHealthChecker(connectors, monitor, new FixedClock(DateTimeOffset.UtcNow), NullLogger<IntegrationHealthChecker>.Instance);

        await checker.RunOnceAsync();

        Assert.Equal("Unreachable", monitor.GetSnapshot("openbao")!.Status);
        Assert.Equal("Reachable", monitor.GetSnapshot("awx")!.Status);
    }
}
